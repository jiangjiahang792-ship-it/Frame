using Logger;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using Sunny.UI;
using Sunny.UI.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

namespace TDJS_Vision.Node._3_Detection.TDAI.Parse
{
    /// <summary>
    /// 瑞领12类端子模型
    /// </summary>
    public class RL12Parse
    {
        /// <summary>
        /// 端子数量检测项内部键，作为除飞丝、飞胶外其它检测项的公共数量上下限配置。
        /// </summary>
        private const string 端子数量内部键 = "DetectItem.TerminalCount";

        /// <summary>
        /// 飞丝检测项内部键，飞丝按自身存在即NG逻辑处理，不参与端子数量公共校验。
        /// </summary>
        private const string 飞丝内部键 = "DetectItem.FlyingWire";

        /// <summary>
        /// 飞胶检测项使用中文键，与飞丝一样独立计数，不参与端子数量公共校验。
        /// </summary>
        private const string 飞胶内部键 = "飞胶";

        /// <summary>前铆脚露线芯数量检测项内部键，对应模型类别0。</summary>
        private const string 前铆脚露线芯数量内部键 = "DetectItem.FrontRivetCoreCount";

        /// <summary>前铆脚数量检测项内部键，对应模型类别1。</summary>
        private const string 前铆脚数量内部键 = "DetectItem.FrontRivetCount";

        /// <summary>后铆脚数量检测项内部键，对应模型类别4。</summary>
        private const string 后铆脚数量内部键 = "DetectItem.BackRivetCount";

        /// <summary>后铆脚露线芯数量检测项内部键，对应模型类别2。</summary>
        private const string 后铆脚露线芯数量内部键 = "DetectItem.BackRivetCoreCount";

        /// <summary>
        /// 正插防水栓主体在RL线芯模型中的类别编号，通信新配方首次学习时据此选择检测项。
        /// </summary>
        private const int 正插防水栓类别编号 = 5;

        /// <summary>
        /// 咬胶长度检测项内部键；检出防水栓时，新配方不启用该互斥检测项。
        /// </summary>
        private const string 咬胶长度内部键 = "DetectItem.BiteGlueLength";

        /// <summary>
        /// 后铆脚露线芯长度内部键；检出防水栓时与咬胶项目一并停用。
        /// </summary>
        private const string 后铆脚露线芯长度内部键 = "DetectItem.BackRivetCoreLength";

        /// <summary>
        /// 用于记录检测结果数量，自动学习模式下使用
        /// </summary>
        static int _count = 0;

        /// <summary>
        /// 用于自动学习模式下存储检测项数据
        /// </summary>
        private static Dictionary<string, List<float>> _autoStudyDatas = new Dictionary<string, List<float>>();

        /// <summary>
        /// AST合压端子结果解析
        /// </summary>
        /// <param name="results"></param>
        /// <param name="resultCount"></param>
        /// <param name="nodeResult"></param>
        public static void Parse(List<DetResult> results, int resultCount, NodeParamTDAI param, Process process, ref NodeResultTDAI nodeResult)
        {
            // 当前使用的检测项配置
            var deteItems = Solution.Instance.DetectItemDic[param.CurDetectItemName];
            ParseCommon.NormalizeDetectItems(deteItems);
            ApplyInitialCommunicationRecipeItemRules(results, param, deteItems);
            DetectItemInfo 端子数量检测项 = 获取端子数量检测项(deteItems);
            int requiredCount = 0;
            if (端子数量检测项 != null && double.TryParse(端子数量检测项.MinValue, out double minimum))
            {
                if (double.IsNaN(minimum) || double.IsInfinity(minimum) || minimum > 10000)
                    throw new InvalidOperationException("端子数量下限必须是0～10000范围内的有效数值。");
                requiredCount = minimum > 0 ? (int)Math.Ceiling(minimum) : 0;
            }

            // 用来保存检测结果的检测项OK/NG、带颜色的框、带颜色的文本
            var detectResults = new Dictionary<string, List<SingleDetectResult>>();
            var texts = new List<ColorText>();
            HashSet<string> addedRectKeys = new HashSet<string>(); // 非数量型仍需去重
            int 端子数量当前值 = 0;

            string str = "";
            for (int i = 0; i < results.Count; i++)
            {
                str += $"下标:{results[i].ClassId}  Box:{results[i].Box.X},{results[i].Box.Y},{results[i].Box.Width},{results[i].Box.Height} ---";
            }
            LogHelper.AddLog(MsgLevel.Info, $"流程{process.ProcessName}AI检测结果值:{str} {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}！", true);


            foreach (var item in deteItems)
            {
                List<(int classId, Rect value)> valuePairs = new List<(int classId, Rect value)>();    //不是数量型检测项数据收集

                string itemName = item.Name;
                bool isCountItem = item.IsCountItem;
                // 检测项没启用就不用处理
                if (!item.Enable) continue;

                // 四个端子部件数量项固定按目标框数量判断，避免配置文件误取消“数量型”后被当作尺寸处理。
                if (IsTerminalComponentCountItem(itemName))
                {
                    item.IsCountItem = true;
                    isCountItem = true;
                }

                // 端子数量只作为除飞丝、飞胶外其它检测项的公共数量上下限，不单独绑定模型类别解析。
                if (itemName == 端子数量内部键)
                    continue;

                bool hasData = false;

                #region 提取函数定义，某个检测项的值取自哪个class_id结果的什么属性？

                Func<DetResult, string> extractValueFunc = null;

                switch (itemName)
                {
                    // 前铆脚长度取类别1的检测框尺寸，统一参与换算、判定和学习。
                    case "前铆脚长度":
                        extractValueFunc = r => r.ClassId == 1 ? r.Box.Width.ToString("F2") : null;
                        break;
                    // 前铆脚宽度取类别1的检测框尺寸，统一参与换算、判定和学习。
                    case "前铆脚宽度":
                        extractValueFunc = r => r.ClassId == 1 ? r.Box.Height.ToString("F2") : null;
                        break;
                    // 后铆脚长度取类别4的检测框尺寸，统一参与换算、判定和学习。
                    case "后铆脚长度":
                        extractValueFunc = r => r.ClassId == 4 ? r.Box.Width.ToString("F2") : null;
                        break;
                    // 后铆脚宽度取类别4的检测框尺寸，统一参与换算、判定和学习。
                    case "后铆脚宽度":
                        extractValueFunc = r => r.ClassId == 4 ? r.Box.Height.ToString("F2") : null;
                        break;
                    // 后铆脚露线芯宽度取类别2的检测框尺寸，统一参与换算、判定和学习。
                    case "后铆脚露线芯宽度":
                        extractValueFunc = r => r.ClassId == 2 ? r.Box.Height.ToString("F2") : null;
                        break;
                    case "DetectItem.FrontRivetCoreLength":
                        extractValueFunc = r => r.ClassId == 0 ? r.Box.Width.ToString("F2") : null;
                        break;
                    case "前铆脚露线芯宽度":
                    case "DetectItem.FrontRivetCoreHeight":
                        extractValueFunc = r => r.ClassId == 0 ? r.Box.Height.ToString("F2") : null;
                        break;
                    case "DetectItem.FrontRivetArea":
                        extractValueFunc = r => r.ClassId == 1 ? (r.Box.Width * r.Box.Height).ToString("F2") : null;
                        break;
                    case "DetectItem.BackRivetCoreLength":
                        extractValueFunc = r => r.ClassId == 2 ? r.Box.Width.ToString("F2") : null;
                        break;
                    case "DetectItem.BackRivetArea":
                        extractValueFunc = r => r.ClassId == 4 ? (r.Box.Width * r.Box.Height).ToString("F2") : null;
                        break;
                    case 前铆脚露线芯数量内部键:
                        extractValueFunc = r => r.ClassId == 0 ? "1" : null;
                        break;
                    case 前铆脚数量内部键:
                        extractValueFunc = r => r.ClassId == 1 ? "1" : null;
                        break;
                    case 后铆脚数量内部键:
                        extractValueFunc = r => r.ClassId == 4 ? "1" : null;
                        break;
                    case 后铆脚露线芯数量内部键:
                        extractValueFunc = r => r.ClassId == 2 ? "1" : null;
                        break;
                    case "DetectItem.ForwardWaterproofPlugArea":
                        extractValueFunc = r => r.ClassId == 5 ? (r.Box.Width * r.Box.Height).ToString("F2") : null;
                        break;
                    case "DetectItem.FlyingWire":
                        // 飞丝勾选数量型时按目标个数统计；取消数量型时按每个检测框的宽×高输出面积。
                        // 两种模式复用后续的上下限、比例换算、补偿、学习和结果绘制流程。
                        extractValueFunc = r => r.ClassId == 8
                            ? (isCountItem ? "1" : (r.Box.Width * r.Box.Height).ToString("F2"))
                            : null;
                        break;
                    case 飞胶内部键:
                        // fly_rubber 为类别9，每个模型结果计为一个飞胶，沿用数量型上下限判断。
                        extractValueFunc = r => r.ClassId == 9 ? "1" : null;
                        break;
                    case "DetectItem.ReverseWaterproofPlug":
                        extractValueFunc = r => r.ClassId == 10 ? "1" : null;
                        break;
                    case "DetectItem.WaterproofPlugDamaged":
                        extractValueFunc = r => r.ClassId == 11 ? "1" : null;
                        break;

                    case "DetectItem.BiteGlueLength":
                        extractValueFunc = r =>
                        {
                            // 咬胶长度由后铆脚右边缘与后铆脚露线芯左边缘之间的水平距离计算。
                            // 类别4作为依赖框保存，类别2作为每一项咬胶测量的基准框。
                            if (r.ClassId == 4)
                            {
                                valuePairs.Add((r.ClassId, r.Box));
                            }
                            return r.ClassId == 2 ? "0" : null;
                        };

                        break;

                    case "DetectItem.CoreLength":
                        extractValueFunc = r => r.ClassId == 7 ? r.Box.Width.ToString("F2") : null;
                        break;

                    case "DetectItem.CoreHeight":
                        extractValueFunc = r => r.ClassId == 7 ? r.Box.Height.ToString("F2") : null;
                        break;

                    case "DetectItem.CoreCount":
                        extractValueFunc = r => r.ClassId == 7 ? "1" : null;
                        break;

                    case "胶皮数量":
                        extractValueFunc = r => r.ClassId == 6 ? "1" : null;
                        break;

                    case "防水栓数量":
                        extractValueFunc = r => r.ClassId == 5 ? "1" : null;
                        break;

                    case "胶皮宽度":
                        // only_rubber 为类别6，取模型框的水平宽度，后续统一按比例换算。
                        extractValueFunc = r => r.ClassId == 6 ? r.Box.Width.ToString("F2") : null;
                        break;

                    case "胶皮高度":
                        // 与胶皮宽度取自同一类别，取模型框的垂直高度。
                        extractValueFunc = r => r.ClassId == 6 ? r.Box.Height.ToString("F2") : null;
                        break;

                    case "DetectItem.ForwardWaterproofPlugLength":
                        extractValueFunc = r => r.ClassId == 5 ? r.Box.Width.ToString("F2") : null;
                        break;

                    case "正插防水栓宽度":
                    case "DetectItem.ForwardWaterproofPlugHeight":
                        extractValueFunc = r => r.ClassId == 5 ? r.Box.Height.ToString("F2") : null;
                        break;

                    default:
                        extractValueFunc = null;
                        break;
                }

                if (extractValueFunc == null)
                    continue;

                #endregion

                #region 匹配结果提取
                var matchedResults = results.Where(r =>
                {
                    string v = extractValueFunc(r);
                    return v != null;
                }).ToList();

                #endregion

                // 独立数量项使用自身上下限，不参与端子数量的公共校验。
                if (!IsIndependentCountItem(itemName))
                    端子数量当前值 = Math.Max(端子数量当前值, matchedResults.Count);

                var singleResults = new List<SingleDetectResult>();
                bool countLimitOk = IsIndependentCountItem(itemName)
                    ? true
                    : 端子数量检测项 == null || ParseCommon.IsValueWithinLimits(端子数量检测项, matchedResults.Count);

                #region 正常情况: 检测项已启用，模型有检出该项的DetResult结果

                if (isCountItem)
                {
                    // 数量型检测项：统计总数，只添加一次结果
                    int count = matchedResults.Count;
                    string valueStr = count.ToString();
                    bool isOk = false;

                    // 添加自动学习数据
                    if (param.IsAutoStudy)
                    {
                        if (!_autoStudyDatas.ContainsKey(itemName))
                            _autoStudyDatas.Add(itemName, new List<float>());
                        _autoStudyDatas[itemName].Add(count);
                    }

                    #region 判断检测项是否OK

                    // 数量型检测项没有检出时按0参与上下限判断，支持0~0直接判OK。
                    valueStr = count.ToString();
                    isOk = ParseCommon.IsValueWithinLimits(item, count) && countLimitOk;

                    #endregion

                    #region 添加检测项结果

                    var singleResult = new SingleDetectResult
                    {
                        Name = itemName,
                        Value = valueStr,
                        IsOk = param.IsAutoStudy ? true : isOk // 如果是自动学习模式，则结果默认标记为OK
                    };
                    singleResults.Add(singleResult);

                    #endregion

                    #region 添加矩形框

                    if (count > 0)
                    {
                        foreach (var aiRes in matchedResults)
                        {
                            // 添加不重复矩形框
                            ParseCommon.AddRect(addedRectKeys, aiRes.Box, param.IsAutoStudy ? true : isOk, ref nodeResult);
                        }
                    }

                    #endregion

                    #region 添加文本信息

                    var isEnable = ParseCommon.GetItemEnableStatus(deteItems, itemName);
                    if (isEnable && !texts.Any(t => t.Text.StartsWith($"{ParseCommon.GetDisplayName(itemName)}:")))
                    {
                        texts.Add(new ColorText(
                            ParseCommon.GetDisplayName(itemName) + ": " + valueStr,
                            (param.IsAutoStudy ? true : isOk) ? Color.Green : Color.Red
                        ));
                    }

                    #endregion

                    // 设置当前检测项的值到结果中
                    ParseCommon.SetDetectItemCurValue(ref param, itemName, valueStr);

                    hasData = true;
                }
                else
                {
                    bool allOk = true;
                    var valueStr = "";

                    foreach (var result in matchedResults)
                    {
                        string rawValue = extractValueFunc(result);
                        if (!float.TryParse(rawValue, out float value))
                            continue;

                        // 默认绘制模型自身检测框；咬胶长度改为绘制两个测量边之间的实际区域。
                        Rect measurementRect = result.Box;

                        if (itemName == "DetectItem.BiteGlueLength")
                        {
                            // 多组端子时按水平方向最近的后铆脚配对，避免使用其它端子的依赖框。
                            Rect? behindFoot = valuePairs
                                .Where(v => v.classId == 4)
                                .Select(v => (Rect?)v.value)
                                .OrderBy(v => Math.Abs(v.Value.Right - result.Box.Left))
                                .FirstOrDefault();
                            if (behindFoot.HasValue)
                            {
                                value = Math.Abs(behindFoot.Value.Right - result.Box.Left);
                                rawValue = value.ToString("F2");
                                measurementRect = 创建咬胶测量区域(behindFoot.Value, result.Box);
                            }
                            else
                            {
                                // 缺少后铆脚依赖框时仅将当前检测项判为NG，继续输出其它检测结果。
                                allOk = false;
                                value = 0;
                                rawValue = "0";
                            }
                        }

                        // 是否需要转成物理值
                        if (param.NeedConvert)
                        {
                            value = value * param.Scale;
                            rawValue = value.ToString("F2"); // 保留两位小数
                        }



                        bool measurementOk = ParseCommon.IsValueWithinLimits(item, value);
                        bool isOk = measurementOk && (countLimitOk || matchedResults.Count < requiredCount);

                        // 一旦有一个 false，则整体不是 OK
                        if (!isOk)
                            allOk = false;

                        var singleResult = new SingleDetectResult
                        {
                            Name = itemName,
                            Value = rawValue,
                            IsOk = param.IsAutoStudy ? true : isOk // 如果是自动学习模式，则结果默认标记为OK
                        };
                        valueStr += (value.ToString("F2") + ","); // 拼接所有值
                        singleResults.Add(singleResult);

                        // 添加自动学习数据
                        if (param.IsAutoStudy)
                        {
                            if (!_autoStudyDatas.ContainsKey(itemName))
                                _autoStudyDatas.Add(itemName, new List<float>());
                            _autoStudyDatas[itemName].Add(value);
                        }

                        // 添加不重复矩形框
                        // 实测框只体现自身尺寸状态，缺件另以补零结果标NG。
                        ParseCommon.AddRect(addedRectKeys, measurementRect, param.IsAutoStudy || measurementOk, ref nodeResult);

                        hasData = true;
                    }

                    if (matchedResults.Count>0)
                    {
                        // -------- 添加文本显示 --------0
                        texts.Add(new ColorText(
                            LanguageManager.Format("{0}: {1} 范围值: [{2}, {3}]", ParseCommon.GetDisplayName(itemName), (matchedResults.Count > 0 ? valueStr : "0"), item.MinValue, item.MaxValue),
                            (param.IsAutoStudy ? true : allOk) ? Color.Green : Color.Red
                        ));

                        // 设置当前检测项的值到结果中
                        ParseCommon.SetDetectItemCurValue(ref param, itemName, (matchedResults.Count > 0 ? valueStr.TrimEnd(',') : "0"));
                    }
                }

                #endregion


                #region 特殊情况处理：检测项已启用，但模型没有检出一个该项的DetResult结果

                // 对于数量型检测项，在正常流程中已处理了这种情况（valueStr = "-1", isOk = false）

                if (!matchedResults.Any() && item.Enable && !isCountItem)
                {
                    bool zeroIsOk = ParseCommon.IsValueWithinLimits(item, 0F) && countLimitOk;

                    #region 对于“存在”即NG的非数量型检测项要这样设置
                    
                    #endregion

                    #region 对于“不存在”即NG的非数量型检测项要这样设置

                    if (matchedResults.Count<=0)
                    {
                        var singleResult = new SingleDetectResult
                        {
                            Name = itemName,
                            Value = "0",
                            IsOk = param.IsAutoStudy ? true : zeroIsOk
                        };
                        singleResults.Add(singleResult);

                        // 添加文本
                        var isEnable = ParseCommon.GetItemEnableStatus(deteItems, itemName);
                        if (isEnable && !texts.Any(t => t.Text.StartsWith($"{ParseCommon.GetDisplayName(itemName)}:")))
                        {
                            texts.Add(new ColorText(
                                LanguageManager.Format("{0}: 0 范围值: [{1}, {2}]", ParseCommon.GetDisplayName(itemName), item.MinValue, item.MaxValue),
                                (param.IsAutoStudy ? true : zeroIsOk) ? Color.Green : Color.Red
                            ));
                        }

                        ParseCommon.SetDetectItemCurValue(ref param, itemName, "0");
                        hasData = true;
                    }

                    #endregion
                }

                #endregion

                if (!isCountItem && !IsIndependentCountItem(itemName) && matchedResults.Count < requiredCount)
                {
                    // 原无结果分支已生成单个0，替换为实际缺失数量；补零不生成框、不参与学习。
                    if (matchedResults.Count == 0) singleResults.Clear();
                    for (int missing = matchedResults.Count; missing < requiredCount; missing++)
                        singleResults.Add(new SingleDetectResult { Name = itemName, Value = "0", IsOk = param.IsAutoStudy });
                    string displayValue = string.Join(",", singleResults.Select(r => r.Value));
                    string displayName = ParseCommon.GetDisplayName(itemName);
                    texts.RemoveAll(t => t.Text.StartsWith(displayName + ":"));
                    texts.Add(new ColorText(LanguageManager.Format("{0}: {1} 范围值: [{2}, {3}]",
                        displayName, displayValue, item.MinValue, item.MaxValue), param.IsAutoStudy ? Color.Green : Color.Red));
                    ParseCommon.SetDetectItemCurValue(ref param, itemName, displayValue);
                    hasData = true;
                }

                // 添加一个检测项的结果数据
                if (hasData)
                {
                    detectResults[itemName] = singleResults;
                }
            }

            写入端子数量检测项结果(端子数量检测项, 端子数量当前值, deteItems, param.IsAutoStudy, ref param, detectResults, texts);

            // 更新 AlgorithmResult
            nodeResult.AlgorithmResult.DetectResults = detectResults;
            
            //写入当前结果
            bool allOk2 = detectResults.Values
                .SelectMany(list => list)     // 展开所有 List
                .All(r => r.IsOk);            // 只要有一个 false 就返回 false


            if (allOk2)
            {
                process.OKNumber++;
            }
            else process.NGNumber++;

            // 客户要求隐藏图像上的总数、良品数和良品率，保留流程内部统计。
            // texts.Add(new ColorText(LanguageManager.Format("Result.TotalCount", process.OKNumber + process.NGNumber), Color.Green));
            // texts.Add(new ColorText(LanguageManager.Format("Result.OKCount", process.OKNumber), Color.Green));
            // texts.Add(new ColorText(LanguageManager.Format("Result.Yield", ((process.OKNumber * 1.0) / (process.OKNumber + process.NGNumber) * 100).ToString("F2")), Color.Green));

            //nodeResult.AlgorithmResult.Rects = rects;
            nodeResult.AlgorithmResult.Texts = texts;

            // 自动学习模式下
            if (param.IsAutoStudy)
            {
                // _count 自增
                _count++;
                // 自动学习上下限完成
                if (param.StudyNum <= _count)
                {
                    try
                    {
                        // 设置检测项的上下限
                        ParseCommon.SetDetectItemMinMax(deteItems, _autoStudyDatas, param.StudyPercentage);
                        ParseCommon.CloseAutoStudyEvent?.Invoke(null, param.NodeName);
                        _count = 0; // 重置计数器
                    }
                    catch (Exception)
                    {
                        _count = 0;
                        throw;
                    }
                    return;
                }
            }
        }

        /// <summary>
        /// 通信新配方首次学习时，根据当前图像是否检出防水栓自动选择互斥检测项。
        /// 没有防水栓时停用全部防水栓相关项；存在防水栓时停用咬胶长度和后铆脚露线芯相关项。
        /// </summary>
        /// <param name="results">当前图像的RL模型检测框。</param>
        /// <param name="param">当前目标分类节点参数。</param>
        /// <param name="detectItems">当前通信新配方检测项。</param>
        internal static void ApplyInitialCommunicationRecipeItemRules(
            IEnumerable<DetResult> results,
            NodeParamTDAI param,
            IList<DetectItemInfo> detectItems)
        {
            if (param == null || param.ModelName != ModelName.RL_12类线芯模型 ||
                !param.IsAutoStudy || detectItems == null ||
                !detectItems.Any(item => item != null && item.NeedsInitialAutoStudy))
            {
                return;
            }

            bool hasWaterproofPlug = results != null &&
                results.Any(result => result.ClassId == 正插防水栓类别编号);

            foreach (DetectItemInfo item in detectItems)
            {
                if (item == null)
                    continue;

                string normalizedName = DetectItemLanguage.NormalizeName(item.Name);
                if (hasWaterproofPlug)
                {
                    if (string.Equals(normalizedName, 咬胶长度内部键, StringComparison.Ordinal) ||
                        IsBackRivetExposedCoreDetectItem(normalizedName))
                        item.Enable = false;
                }
                else if (IsWaterproofPlugDetectItem(normalizedName))
                {
                    item.Enable = false;
                }
            }

            LogHelper.AddLog(
                MsgLevel.Info,
                hasWaterproofPlug
                    ? $"通信新配方({param.CurDetectItemName})首次学习检出防水栓，已停用咬胶长度及后铆脚露线芯相关检测项。"
                    : $"通信新配方({param.CurDetectItemName})首次学习未检出防水栓，已停用全部防水栓相关检测项。",
                true);
        }

        /// <summary>判断检测项是否属于后铆脚露线芯长度或宽度项目。</summary>
        private static bool IsBackRivetExposedCoreDetectItem(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName))
                return false;

            return string.Equals(itemName, 后铆脚露线芯长度内部键, StringComparison.Ordinal) ||
                   itemName.IndexOf("后铆脚露线芯", StringComparison.Ordinal) >= 0;
        }

        /// <summary>判断检测项是否属于RL线芯模型的防水栓相关项目。</summary>
        private static bool IsWaterproofPlugDetectItem(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName))
                return false;

            return itemName.IndexOf("防水栓", StringComparison.Ordinal) >= 0 ||
                   string.Equals(itemName, "DetectItem.ForwardWaterproofPlugArea", StringComparison.Ordinal) ||
                   string.Equals(itemName, "DetectItem.ReverseWaterproofPlug", StringComparison.Ordinal) ||
                   string.Equals(itemName, "DetectItem.WaterproofPlugDamaged", StringComparison.Ordinal) ||
                   string.Equals(itemName, "DetectItem.ForwardWaterproofPlugLength", StringComparison.Ordinal) ||
                   string.Equals(itemName, "DetectItem.ForwardWaterproofPlugHeight", StringComparison.Ordinal);
        }

        /// <summary>
        /// 获取启用的端子数量公共检测项配置。
        /// </summary>
        /// <param name="detectItemInfos">当前方案检测项集合。</param>
        /// <returns>启用的端子数量配置；未配置时返回 null，保持旧方案行为。</returns>
        private static DetectItemInfo 获取端子数量检测项(List<DetectItemInfo> detectItemInfos)
        {
            if (detectItemInfos == null)
                return null;

            return detectItemInfos.FirstOrDefault(item => item.Enable
                && string.Equals(item.Name, 端子数量内部键, StringComparison.Ordinal));
        }

        /// <summary>
        /// 写入端子数量检测项结果和绘制文本，供ROI结果绘制按检测项订阅输出。
        /// </summary>
        /// <param name="item">启用的端子数量配置。</param>
        /// <param name="count">除飞丝、飞胶外检测项匹配数量的代表值。</param>
        /// <param name="detectItemInfos">当前方案检测项集合。</param>
        /// <param name="isAutoStudy">是否处于自动学习模式。</param>
        /// <param name="param">AI检测节点参数。</param>
        /// <param name="detectResults">检测项结果集合。</param>
        /// <param name="texts">待绘制文本集合。</param>
        private static void 写入端子数量检测项结果(
            DetectItemInfo item,
            int count,
            List<DetectItemInfo> detectItemInfos,
            bool isAutoStudy,
            ref NodeParamTDAI param,
            Dictionary<string, List<SingleDetectResult>> detectResults,
            List<ColorText> texts)
        {
            if (item == null)
                return;

            string valueStr = count.ToString();
            bool isOk = ParseCommon.IsValueWithinLimits(item, count);

            if (isAutoStudy)
            {
                if (!_autoStudyDatas.ContainsKey(端子数量内部键))
                    _autoStudyDatas.Add(端子数量内部键, new List<float>());
                _autoStudyDatas[端子数量内部键].Add(count);
            }

            detectResults[端子数量内部键] = new List<SingleDetectResult>
            {
                new SingleDetectResult
                {
                    Name = 端子数量内部键,
                    Value = valueStr,
                    IsOk = isAutoStudy ? true : isOk
                }
            };

            if (ParseCommon.GetItemEnableStatus(detectItemInfos, 端子数量内部键)
                && !texts.Any(t => t.Text.StartsWith($"{ParseCommon.GetDisplayName(端子数量内部键)}:")))
            {
                texts.Insert(0, new ColorText(
                    ParseCommon.GetDisplayName(端子数量内部键) + ": " + valueStr,
                    (isAutoStudy ? true : isOk) ? Color.Green : Color.Red
                ));
            }

            ParseCommon.SetDetectItemCurValue(ref param, 端子数量内部键, valueStr);
        }

        /// <summary>
        /// 判断当前检测项是否使用自身上下限独立校验数量。
        /// </summary>
        /// <param name="itemName">当前检测项内部键。</param>
        /// <returns>独立计数项返回 true。</returns>
        private static bool IsIndependentCountItem(string itemName)
        {
            return string.Equals(itemName, 飞丝内部键, StringComparison.Ordinal)
                || string.Equals(itemName, 飞胶内部键, StringComparison.Ordinal)
                || string.Equals(itemName, "DetectItem.CoreCount", StringComparison.Ordinal)
                || string.Equals(itemName, "胶皮数量", StringComparison.Ordinal)
                || string.Equals(itemName, "防水栓数量", StringComparison.Ordinal)
                || IsTerminalComponentCountItem(itemName);
        }

        /// <summary>
        /// 判断是否为RL模型新增的端子部件数量检测项。
        /// </summary>
        /// <param name="itemName">当前检测项内部键。</param>
        /// <returns>属于四个端子部件数量项时返回 true。</returns>
        private static bool IsTerminalComponentCountItem(string itemName)
        {
            return string.Equals(itemName, 前铆脚露线芯数量内部键, StringComparison.Ordinal)
                || string.Equals(itemName, 前铆脚数量内部键, StringComparison.Ordinal)
                || string.Equals(itemName, 后铆脚数量内部键, StringComparison.Ordinal)
                || string.Equals(itemName, 后铆脚露线芯数量内部键, StringComparison.Ordinal);
        }

        /// <summary>
        /// 创建咬胶长度的可视化测量区域，横向严格覆盖后铆脚右边缘到露线芯左边缘。
        /// </summary>
        /// <param name="behindFoot">类别4后铆脚检测框。</param>
        /// <param name="exposedCore">类别2后铆脚露线芯检测框。</param>
        /// <returns>用于结果绘制的咬胶测量矩形。</returns>
        private static Rect 创建咬胶测量区域(Rect behindFoot, Rect exposedCore)
        {
            int left = Math.Min(behindFoot.Right, exposedCore.Left);
            int width = Math.Max(1, Math.Abs(behindFoot.Right - exposedCore.Left));

            // 优先使用两个框的垂直重叠区域，使ROI落在真实咬胶位置；无重叠时覆盖两框之间的完整高度。
            int top = Math.Max(behindFoot.Top, exposedCore.Top);
            int bottom = Math.Min(behindFoot.Bottom, exposedCore.Bottom);
            if (bottom <= top)
            {
                top = Math.Min(behindFoot.Top, exposedCore.Top);
                bottom = Math.Max(behindFoot.Bottom, exposedCore.Bottom);
            }

            return new Rect(left, top, width, Math.Max(1, bottom - top));
        }

        /// <summary>
        /// 辅助方法
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static Rect GetDistanceRect(Rect a, Rect b)
        {
            // 辅助数值
            int aRight = a.X + a.Width;
            int bRight = b.X + b.Width;
            int aBottom = a.Y + a.Height;
            int bBottom = b.Y + b.Height;

            // 判断水平完全不重叠 → 水平距离矩形
            if (aRight < b.X || bRight < a.X)
            {
                // 水平间距
                int left = Math.Min(aRight, bRight);
                int right = Math.Max(a.X, b.X);

                // 垂直方向取重叠部分
                int top = Math.Max(a.Y, b.Y);
                int bottom = Math.Min(aBottom, bBottom);

                return new Rect(
                    left,
                    top,
                    right - left,
                    Math.Max(0, bottom - top)
                );
            }

            // 判断垂直完全不重叠 → 垂直距离矩形
            if (aBottom < b.Y || bBottom < a.Y)
            {
                // 垂直间距
                int top = Math.Min(aBottom, bBottom);
                int bottom = Math.Max(a.Y, b.Y);

                // 水平方向取重叠部分
                int left = Math.Max(a.X, b.X);
                int right = Math.Min(aRight, bRight);

                return new Rect(
                    left,
                    top,
                    Math.Max(0, right - left),
                    bottom - top
                );
            }

            // ✅ 斜向情况 → 自动判断哪个方向距离更短
            int horizontalDist = 0;
            if (aRight < b.X) horizontalDist = b.X - aRight;
            else if (bRight < a.X) horizontalDist = a.X - bRight;

            int verticalDist = 0;
            if (aBottom < b.Y) verticalDist = b.Y - aBottom;
            else if (bBottom < a.Y) verticalDist = a.Y - bBottom;

            // 若水平更短 → 水平距离矩形
            if (horizontalDist <= verticalDist)
            {
                int left = Math.Min(aRight, bRight);
                int right = Math.Max(a.X, b.X);

                int top = Math.Max(a.Y, b.Y);
                int bottom = Math.Min(aBottom, bBottom);

                return new Rect(
                    left,
                    top,
                    right - left,
                    Math.Max(0, bottom - top)
                );
            }
            else // 垂直更短 → 垂直距离矩形
            {
                int top = Math.Min(aBottom, bBottom);
                int bottom = Math.Max(a.Y, b.Y);

                int left = Math.Max(a.X, b.X);
                int right = Math.Min(aRight, bRight);

                return new Rect(
                    left,
                    top,
                    Math.Max(0, right - left),
                    bottom - top
                );
            }
        }

        public static double GetRegionOverlapPercentage(Rect A, Rect B)
        {
            Rect inter = A & B;       // 直接取交集

            // 没有交集，直接返回0
            if (inter.Width <= 0 || inter.Height <= 0)
                return 0;

            // A 面积
            double areaA = A.Width * A.Height;
            if (areaA <= 0) return 0;

            // 交集面积
            double interArea = inter.Width * inter.Height;

            // 占比 = 交集面积 / A 面积
            return interArea / areaA * 100.0; // 返回百分比

        }
    }
}
