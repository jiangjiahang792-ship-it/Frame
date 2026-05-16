using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Logger;
using OpenCvSharp;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

namespace TDJS_Vision.Node._3_Detection.TDAI.Parse
{
    public class USBSegClass6
    {
        /// <summary>
        /// 用于记录检测结果数量，自动学习模式下使用
        /// </summary>
        static int _count = 0;
        static float ReferenceValue;

        /// <summary>
        /// 用于自动学习模式下存储检测项数据
        /// </summary>
        private static Dictionary<string, List<float>> _autoStudyDatas = new Dictionary<string, List<float>>();

        /// <summary>
        /// 解析线芯截面模型结果
        /// </summary>
        /// <param name="results"></param>
        /// <param name="resultCount"></param>
        /// <param name="nodeResult"></param>
        public static void Parse(List<SegResult> results, int resultCount, NodeParamTDAI param, ref NodeResultTDAI nodeResult)
        {
            // 当前使用的检测项配置
            var deteItems = Solution.Instance.DetectItemDic[param.CurDetectItemName];
            ParseCommon.NormalizeDetectItems(deteItems);

            // 用来保存检测结果的检测项OK/NG、带颜色的框、带颜色的文本
            var detectResults = new Dictionary<string, List<SingleDetectResult>>();
            var texts = new List<ColorText>();

            HashSet<string> addedRectKeys = new HashSet<string>(); // 非数量型仍需去重

            foreach (var item in deteItems)
            {
                // 检测项没启用就不用处理
                if (!item.Enable) continue;

                string itemName = item.Name;
                bool hasData = false;

                #region 提取函数定义，某个检测项的值取自哪个class_id结果的什么属性？

                Func<SegResult, string> extractValueFunc = null;
                
                switch (itemName)
                {
                    case "DetectItem.ACPlugCount":
                        extractValueFunc = r => r.ClassId == 0 ? "1" : null;
                        break;
                    case "DetectItem.ACPlugInPlace":
                        extractValueFunc = r => r.ClassId == 0 ? $"{r.Box.X}-{r.Box.Y}-{r.Box.Width}-{r.Box.Height}" : null;
                        break;
                    case "DetectItem.ACTailCount":
                        extractValueFunc = r => r.ClassId == 1 ? "1" : null;
                        break;
                    case "DetectItem.ACTailInPlace":
                        extractValueFunc = r => r.ClassId == 1 ? $"{r.Box.X}-{r.Box.Y}-{r.Box.Width}-{r.Box.Height}" : null;
                        break;                    
                    case "DetectItem.WireInSlot":
                        extractValueFunc = r => r.ClassId == 2 ? "1" : null;
                        break;
                    case "DetectItem.WireOutOfSlot":
                        extractValueFunc = r => r.ClassId == 3 ? "1" : null;
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
                    string value = extractValueFunc(r);
                    return value != null;
                }).ToList();

                #endregion

                var singleResults = new List<SingleDetectResult>();

                #region 正常情况: 检测项已启用，模型有检出该项的 SegResult 结果

                // 获取当前检测项是否为“数量型”
                bool isCountItem = item.IsCountItem;

                if (isCountItem)
                {
                    // 数量型检测项：统计总数，只添加一次结果
                    int count = matchedResults.Count;
                    string valueStr = count.ToString();
                    bool isOk = false;
                    bool hasClass2 = results != null && results.Any(r => r.ClassId == 2);

                    // 添加自动学习数据
                    if (param.IsAutoStudy)
                    {
                        if (!_autoStudyDatas.ContainsKey(itemName))
                            _autoStudyDatas.Add(itemName, new List<float>());
                        _autoStudyDatas[itemName].Add(count);
                    }

                    List<string> ngCountItems = new List<string>()
                    {
                        "DetectItem.WireOutOfSlot"

                    };
                    #region 判断检测项是否OK

                    if (count > 0)
                    {
                        // 有结果时才判断上下限
                        bool minOk = float.TryParse(item.MinValue, out float min) && count >= min;
                        bool maxOk = float.TryParse(item.MaxValue, out float max) && count <= max;
                        isOk = minOk && maxOk;

                        // 规则覆盖：当存在“线不在槽”(class_id=5)时，“线在槽”(class_id=4)的框必须为绿色
                        if (itemName == "DetectItem.WireInSlot" && hasClass2)
                            isOk = true;

                        // 规则覆盖：当“线不在槽”数量>0时，其框必须为红色
                        if (itemName == "DetectItem.WireOutOfSlot" && count > 0)
                            isOk = false;
                    }
                    else
                    {
                        // 没有结果时，默认值 0，直接标记为 NG
                        valueStr = "0";
                        isOk = false;
                        isOk = ngCountItems.Contains(itemName) ? true : false; // NG型检测项没有结果时标记为OK
                    }

                    #endregion

                    #region 添加检测项结果

                    var singleResult = new SingleDetectResult
                    {
                        Name = itemName,
                        Value = valueStr,
                        IsOk = param.IsAutoStudy ? true : isOk
                    };
                    singleResults.Add(singleResult);

                    #endregion

                    #region 添加矩形框



                    if (count > 0)
                    {
                        foreach (var aiRes in matchedResults)
                        {
                            // “线在槽”在存在 class_id=2 时绿框
                            if (itemName == "DetectItem.WireInSlot" && results != null && results.Any(r => r.ClassId == 2))
                            {
                                ParseCommon.AddRect(addedRectKeys, aiRes.Box, true, ref nodeResult);
                                continue;
                            }
                            // “线不在槽”只要 count>0 红框
                            if (itemName == "DetectItem.WireOutOfSlot" && count > 0)
                            {
                                ParseCommon.AddRect(addedRectKeys, aiRes.Box, false, ref nodeResult);
                                continue;
                            }

                            // 添加不重复矩形框（默认逻辑）
                            ParseCommon.AddRect(addedRectKeys, aiRes.Box, param.IsAutoStudy ? true : isOk, ref nodeResult);
                        }
                    }


                    #endregion

                    #region 添加文本信息

                    var isEnable = ParseCommon.GetItemEnableStatus(deteItems, itemName);
                    if (isEnable && !texts.Any(t => t.Text.StartsWith($"{ParseCommon.GetDisplayName(itemName)}:")))
                    {
                        texts.Add(new ColorText(
                            $"{ParseCommon.GetDisplayName(itemName)}: {((param.IsAutoStudy ? true : isOk) ? "OK" : "NG")}",
                            (param.IsAutoStudy ? true : isOk) ? Color.Green : Color.Red // 如果是自动学习模式，则文本默认标记为绿色OK
                        ));
                    }

                    #endregion

                    // 设置当前检测项的值到结果中
                    ParseCommon.SetDetectItemCurValue(ref param, itemName, valueStr);

                    hasData = true;
                }
                else
                {
                    bool allOk = false;
                    var valueStr = "";                 

                    float.TryParse(item.MaxValue, out float max);

                    List<Rect> rectOKs = new List<Rect>();
                    List<Rect> rectNGs = new List<Rect>();

                    // 根据匹配结果包含的 ClassId 决定调用哪个距离计算方法
                    if (matchedResults.Any(r => r.ClassId == 0))
                    {
                        var r0 = USBDistancesClass0(matchedResults, max, param, ref valueStr, singleResults, itemName);
                        rectOKs.AddRange(r0.Item1);
                        rectNGs.AddRange(r0.Item2);
                    }
                    if (matchedResults.Any(r => r.ClassId == 1))
                    {
                        var r1 = USBDistancesClass1(matchedResults, max, param, ref valueStr, singleResults, itemName);
                        rectOKs.AddRange(r1.Item1);
                        rectNGs.AddRange(r1.Item2);
                    }
                    //if (matchedResults.Any(r => r.ClassId == 2))
                    //{
                    //    var r2 = USBDistancesClass2(matchedResults, max, param, ref valueStr, singleResults, itemName);
                    //    rectOKs.AddRange(r2.Item1);
                    //    rectNGs.AddRange(r2.Item2);
                    //}
                    //if (matchedResults.Any(r => r.ClassId == 3))
                    //{
                    //    var r3 = USBDistancesClass3(matchedResults, max, param, ref valueStr, singleResults, itemName);
                    //    rectOKs.AddRange(r3.Item1);
                    //    rectNGs.AddRange(r3.Item2);
                    //}
                    // 一旦有一个 false，则整体不是 OK
                    if (rectNGs.Count == 0)
                        allOk = true;
                    if (param.IsAutoStudy)
                    {
                        // 自动学习模式下，所有结果框都标记为 OK
                        foreach (var rect in rectOKs)
                        {
                            // 添加OK矩形框
                            ParseCommon.AddRect(addedRectKeys, rect, true, ref nodeResult);
                        }

                        foreach (var rect in rectNGs)
                        {
                            // 添加NG矩形框
                            ParseCommon.AddRect(addedRectKeys, rect, true, ref nodeResult);
                        }
                    }
                    else
                    {
                        // 非自动学习模式下，所有结果框根据实际标记
                        foreach (var rect in rectOKs)
                        {
                            // 添加OK矩形框
                            ParseCommon.AddRect(addedRectKeys, rect, true, ref nodeResult);
                        }

                        foreach (var rect in rectNGs)
                        {
                            // 添加NG矩形框
                            
                            ParseCommon.AddRect(addedRectKeys, rect, false, ref nodeResult);
                        }
                    }
                    // 遍历完成后统一添加文本信息
                    var isEnable = ParseCommon.GetItemEnableStatus(deteItems, itemName);
                    if (isEnable && !texts.Any(t => t.Text.StartsWith($"{ParseCommon.GetDisplayName(itemName)}:")))
                    {
                        texts.Add(new ColorText(
                            $"{ParseCommon.GetDisplayName(itemName)}: {((param.IsAutoStudy ? true : allOk) ? "OK" : "NG")}",
                            (param.IsAutoStudy ? true : allOk) ? Color.Green : Color.Red
                        ));
                    }

                    // 设置当前检测项的值到结果中
                    ParseCommon.SetDetectItemCurValue(ref param, itemName, valueStr.TrimEnd(','));

                    if (singleResults.Count > 0)
                        hasData = true;
                }

                #endregion


                #region 特殊情况处理：检测项已启用，但模型没有检出一个该项的 SegResult 结果

                // 对于数量型检测项，在正常流程中已处理了这种情况（valueStr = "-1", isOk = false）
                // 所以这里只需处理非数量型检测项中的特殊案例

                if (!matchedResults.Any() && item.Enable && !isCountItem)
                {
                    string defaultValue = null;
                    bool isSpecialCaseOk = false;

                    //对于“存在”即NG的检测项要这样设置
                    if (itemName == "DetectItem.WireOutOfSlot")
                    {
                        defaultValue = "0";
                        isSpecialCaseOk = true;
                    }

                    // 对于“不存在”即NG的检测项要这样设置
                    if (defaultValue != null)
                    {
                        var singleResult = new SingleDetectResult
                        {
                            Name = itemName,
                            Value = defaultValue,
                            IsOk = isSpecialCaseOk
                        };
                        singleResults.Add(singleResult);

                        // 添加全局提示文本（例如“未检测到不良焊锡”）
                        texts.Add(new ColorText(
                            $"{ParseCommon.GetDisplayName(itemName)}: {(isSpecialCaseOk ? "OK" : "NG")}",
                            isSpecialCaseOk ? Color.Green : Color.Red
                        ));

                        hasData = true;
                    }
                }

                #endregion

                // 添加一个检测项的结果数据
                if (hasData)
                {
                    detectResults[itemName] = singleResults;
                }
            }

            

            // 更新 AlgorithmResult
            nodeResult.AlgorithmResult.DetectResults = detectResults;
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
        /// 根据基准计算 USB 的 Y 偏移
        /// </summary>
        public static (List<Rect>, List<Rect>) USBDistancesClass0(
            List<SegResult> results,
            float distance,
            NodeParamTDAI param,
            ref string resStr,
            List<SingleDetectResult> singleDetectResults,
            string itemName)
        {
            var inliers = new List<Rect>();
            var outliers = new List<Rect>();

            try
            {
                if (results == null || results.Count == 0)
                    return (new List<Rect>(), new List<Rect>());
                if (results.Count == 1)
                    return (new List<Rect>(), new List<Rect> { results[0].Box });

                // 1. 获取 class_id=0 的第一个结果作为基准
                var ReferenceClass0Result = results.FirstOrDefault(r => r.ClassId == 0);
                float Reference0Y = ReferenceClass0Result.Box.Y;

                // 2. 仅针对 class_id=0 的结果计算与基准的 Y 差值
                var class0Results = results.Where(r => r.ClassId == 0).ToList();
                var DistancesClass0 = class0Results.Select(r => Math.Abs(r.Box.Y - Reference0Y)).ToList();

                for (int i = 0; i < DistancesClass0.Count; i++)
                {
                    bool isOk = false;
                    var rawValue = DistancesClass0[i].ToString("F2");
                    // 是否需要转成物理值
                    if (param.NeedConvert)
                    {
                        DistancesClass0[i] = DistancesClass0[i] * param.Scale;
                        rawValue = DistancesClass0[i].ToString("F2"); // 保留两位小数
                    }

                    // 根据阈值判定 OK/NG 并收集矩形
                    if (DistancesClass0[i] <= distance)
                    {
                        inliers.Add(class0Results[i].Box);
                        isOk = true;
                    }
                    else
                    {
                        outliers.Add(class0Results[i].Box);
                    }

                    // 添加自动学习数据
                    if (param.IsAutoStudy)
                    {
                        if (!_autoStudyDatas.ContainsKey(itemName))
                            _autoStudyDatas.Add(itemName, new List<float>());
                        _autoStudyDatas[itemName].Add(float.Parse(rawValue));
                    }

                    // 添加单个检测结果
                    var singleResult = new SingleDetectResult
                    {
                        Name = itemName,
                        Value = rawValue,
                        IsOk = param.IsAutoStudy ? true : isOk
                    };
                    resStr += (rawValue + ",");
                    singleDetectResults.Add(singleResult);
                }                              
            }
            catch (Exception e)
            {
                LogHelper.AddLog(MsgLevel.Exception, e.Message, true);
            }

            return (inliers, outliers);
        }
        public static (List<Rect>, List<Rect>) USBDistancesClass1(
            List<SegResult> results,
            float distance,
            NodeParamTDAI param,
            ref string resStr,
            List<SingleDetectResult> singleDetectResults,
            string itemName)
        {
            var inliers = new List<Rect>();
            var outliers = new List<Rect>();

            try
            {
                if (results == null || results.Count == 0)
                    return (new List<Rect>(), new List<Rect>());
                if (results.Count == 1)
                    return (new List<Rect>(), new List<Rect> { results[0].Box });

                // 1. 获取 class_id=1 的第一个结果作为基准
                var ReferenceClass1Result = results.FirstOrDefault(r => r.ClassId == 1);
                float Reference1Y = ReferenceClass1Result.Box.Y;

                // 2. 仅针对 class_id=1 的结果计算与基准的 Y 差值
                var class0Results = results.Where(r => r.ClassId == 1).ToList();
                var DistancesClass1 = class0Results.Select(r => Math.Abs(r.Box.Y - Reference1Y)).ToList();

                for (int i = 0; i < DistancesClass1.Count; i++)
                {
                    bool isOk = false;
                    var rawValue = DistancesClass1[i].ToString("F2");
                    // 是否需要转成物理值
                    if (param.NeedConvert)
                    {
                        DistancesClass1[i] = DistancesClass1[i] * param.Scale;
                        rawValue = DistancesClass1[i].ToString("F2"); // 保留两位小数
                    }

                    // 根据阈值判定 OK/NG 并收集矩形
                    if (DistancesClass1[i] <= distance)
                    {
                        inliers.Add(class0Results[i].Box);
                        isOk = true;
                    }
                    else
                    {
                        outliers.Add(class0Results[i].Box);
                    }

                    // 添加自动学习数据
                    if (param.IsAutoStudy)
                    {
                        if (!_autoStudyDatas.ContainsKey(itemName))
                            _autoStudyDatas.Add(itemName, new List<float>());
                        _autoStudyDatas[itemName].Add(float.Parse(rawValue));
                    }

                    // 添加单个检测结果
                    var singleResult = new SingleDetectResult
                    {
                        Name = itemName,
                        Value = rawValue,
                        IsOk = param.IsAutoStudy ? true : isOk
                    };
                    resStr += (rawValue + ",");
                    singleDetectResults.Add(singleResult);
                }
            }
            catch (Exception e)
            {
                LogHelper.AddLog(MsgLevel.Exception, e.Message, true);
            }

            return (inliers, outliers);
        }
    //    public static (List<Rect>, List<Rect>) USBDistancesClass2(
    //        List<SegResult> results,
    //        float distance,
    //        NodeParamTDAI param,
    //        ref string resStr,
    //        List<SingleDetectResult> singleDetectResults,
    //        string itemName)
    //    {
    //        var inliers = new List<Rect>();
    //        var outliers = new List<Rect>();

    //        try
    //        {
    //            if (results == null || results.Count == 0)
    //                return (new List<Rect>(), new List<Rect>());
    //            if (results.Count == 1)
    //                return (new List<Rect>(), new List<Rect> { results[0].Box });

    //            // 1. 获取 class_id=2 的第一个结果作为基准
    //            var ReferenceClass2Result = results.FirstOrDefault(r => r.ClassId == 2);
    //            float Reference2Y = ReferenceClass2Result.Box.Y;
    //            // 2. 仅针对 class_id=2 的结果计算与基准的 Y 差值
    //            var class2Results = results.Where(r => r.ClassId == 2).ToList();
    //            var DistancesClass2 = class2Results.Select(r => Math.Abs(r.Box.Y - Reference2Y)).ToList();

    //            for (int i = 0; i < DistancesClass2.Count; i++)
    //            {
    //                bool isOk = false;
    //                var rawValue = DistancesClass2[i].ToString("F2");
    //                // 是否需要转成物理值
    //                if (param.NeedConvert)
    //                {
    //                    DistancesClass2[i] = DistancesClass2[i] * param.Scale;
    //                    rawValue = DistancesClass2[i].ToString("F2"); // 保留两位小数
    //                }

    //                // 根据阈值判定 OK/NG 并收集矩形
    //                if (DistancesClass2[i] <= distance)
    //                {
    //                    inliers.Add(class2Results[i].Box);
    //                    isOk = true;
    //                }
    //                else
    //                {
    //                    outliers.Add(class2Results[i].Box);
    //                }

    //                // 添加自动学习数据
    //                if (param.IsAutoStudy)
    //                {
    //                    if (!_autoStudyDatas.ContainsKey(itemName))
    //                        _autoStudyDatas.Add(itemName, new List<float>());
    //                    _autoStudyDatas[itemName].Add(float.Parse(rawValue));
    //                }

    //                // 添加单个检测结果
    //                var singleResult = new SingleDetectResult
    //                {
    //                    Name = itemName,
    //                    Value = rawValue,
    //                    IsOk = param.IsAutoStudy ? true : isOk
    //                };
    //                resStr += (rawValue + ",");
    //                singleDetectResults.Add(singleResult);
    //            }

    //        }
    //        catch (Exception e)
    //        {
    //            LogHelper.AddLog(MsgLevel.Exception, e.Message, true);
    //        }

    //        return (inliers, outliers);
    //    }
    //    public static (List<Rect>, List<Rect>) USBDistancesClass3(
    //List<SegResult> results,
    //float distance,
    //NodeParamTDAI param,
    //ref string resStr,
    //List<SingleDetectResult> singleDetectResults,
    //string itemName)
    //    {
    //        var inliers = new List<Rect>();
    //        var outliers = new List<Rect>();

    //        try
    //        {
    //            if (results == null || results.Count == 0)
    //                return (new List<Rect>(), new List<Rect>());
    //            if (results.Count == 1)
    //                return (new List<Rect>(), new List<Rect> { results[0].Box });

    //            // 1. 获取 class_id=3 的第一个结果作为基准
    //            var ReferenceClass3Result = results.FirstOrDefault(r => r.ClassId == 3);
    //            float Reference3Y = ReferenceClass3Result.Box.Y;
    //            // 2. 仅针对 class_id=3 的结果计算与基准的 Y 差值
    //            var class3Results = results.Where(r => r.ClassId == 3).ToList();
    //            var DistancesClass3 = class3Results.Select(r => Math.Abs(r.Box.Y - Reference3Y)).ToList();

    //            for (int i = 0; i < DistancesClass3.Count; i++)
    //            {
    //                bool isOk = false;
    //                var rawValue = DistancesClass3[i].ToString("F2");
    //                // 是否需要转成物理值
    //                if (param.NeedConvert)
    //                {
    //                    DistancesClass3[i] = DistancesClass3[i] * param.Scale;
    //                    rawValue = DistancesClass3[i].ToString("F2"); // 保留两位小数
    //                }

    //                // 根据阈值判定 OK/NG 并收集矩形
    //                if (DistancesClass3[i] <= distance)
    //                {
    //                    inliers.Add(class3Results[i].Box);
    //                    isOk = true;
    //                }
    //                else
    //                {
    //                    outliers.Add(class3Results[i].Box);
    //                }

    //                // 添加自动学习数据
    //                if (param.IsAutoStudy)
    //                {
    //                    if (!_autoStudyDatas.ContainsKey(itemName))
    //                        _autoStudyDatas.Add(itemName, new List<float>());
    //                    _autoStudyDatas[itemName].Add(float.Parse(rawValue));
    //                }

    //                // 添加单个检测结果
    //                var singleResult = new SingleDetectResult
    //                {
    //                    Name = itemName,
    //                    Value = rawValue,
    //                    IsOk = param.IsAutoStudy ? true : isOk
    //                };
    //                resStr += (rawValue + ",");
    //                singleDetectResults.Add(singleResult);
    //            }

    //        }
    //        catch (Exception e)
    //        {
    //            LogHelper.AddLog(MsgLevel.Exception, e.Message, true);
    //        }

    //        return (inliers, outliers);
    //    }
    }
}
