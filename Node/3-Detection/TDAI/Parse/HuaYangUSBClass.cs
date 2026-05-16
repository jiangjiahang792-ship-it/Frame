using Logger;
using Logger;
using OpenCvSharp;
using OpenCvSharp;
using Sunny.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

namespace TDJS_Vision.Node._3_Detection.TDAI.Parse
{
    public class HuaYangUSBClass
    {
        /// <summary>
        /// 用于记录检测结果数量，自动学习模式下使用
        /// </summary>
        static int _count = 0;

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
        public static void Parse(List<SegResult> results, int resultCount, NodeParamTDAI param, Process process, ref NodeResultTDAI nodeResult)
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
                    case "DetectItem.TypeCMetalCount":
                        extractValueFunc = r => r.ClassId == 0 ? "1" : null;
                        break;
                    case "DetectItem.TypeCMetalTop":
                        extractValueFunc = r => r.ClassId == 0 ? $"{r.Box.TopLeft.Y}" : null;
                        break;
                    case "DetectItem.TypeCMetalWidth":
                        extractValueFunc = r => r.ClassId == 0 ? $"{r.Box.Width}" : null;
                        break;
                    case "DetectItem.TypeCMetalHeight":
                        extractValueFunc = r => r.ClassId == 0 ? $"{r.Box.Height }" : null;
                        break;

                    case "DetectItem.TypeCSolderCount":
                        extractValueFunc = r => r.ClassId == 1 ? $"1" : null;
                        break;
                    case "DetectItem.TypeCSolderTop":
                        extractValueFunc = r => r.ClassId == 1 ? $"{r.Box.TopLeft.Y}" : null;
                        break;

                    case "DetectItem.TypeAMetalCount":
                        extractValueFunc = r => r.ClassId == 2 ? "1" : null;
                        break;
                    case "DetectItem.TypeAMetalTop": 
                        extractValueFunc = r => r.ClassId == 2 ? $"{r.Box.Top}" : null;
                        break;
                    case "DetectItem.TypeAMetalWidth":
                        extractValueFunc = r => r.ClassId == 2 ? $"{r.Box.Width}" : null;
                        break;
                    case "DetectItem.TypeAMetalHeight":
                        extractValueFunc = r => r.ClassId == 2 ? $"{r.Box.Height}" : null;
                        break;

                    case "DetectItem.TypeASolderCount":
                        extractValueFunc = r => r.ClassId == 3 ? $"1" : null;
                        break;
                    case "DetectItem.TypeASolderTop":
                        extractValueFunc = r => r.ClassId == 3 ? $"{r.Box.Top}" : null;
                        break;
                        
                    case "DetectItem.WireInSlot":
                        extractValueFunc = r => r.ClassId == 4 ? $"1" : null;
                        break;
                    case "DetectItem.WireOutOfSlot":
                        extractValueFunc = r => r.ClassId == 5 ? $"1" : null;
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

                #region 正常情况: 检测项已启用，模型有检出该项的 ObbResult 结果

                // 获取当前检测项是否为“数量型”
                bool isCountItem = item.IsCountItem;

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

                    if (count > 0)
                    {
                        // 有结果时才判断上下限
                        bool minOk = float.TryParse(item.MinValue, out float min) && count >= min;
                        bool maxOk = float.TryParse(item.MaxValue, out float max) && count <= max;
                        isOk = minOk && maxOk;
                    }
                    else
                    {
                        if (itemName == "DetectItem.WireOutOfSlot")
                        {
                            // 没有结果时，默认值 0，直接标记为 NG
                            valueStr = "0";
                            isOk = true;
                        }
                        else {

                            // 没有结果时，默认值 0，直接标记为 NG
                            valueStr = "0";
                            isOk = false;
                        }
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

                    #region 添加矩形框

                    if (itemName == "DetectItem.TypeCMetalCount" || itemName == "DetectItem.TypeCSolderCount" || itemName == "DetectItem.TypeAMetalCount" || itemName == "DetectItem.TypeASolderCount" || itemName == "DetectItem.WireInSlot")
                    {
                        if (count > 0)
                        {
                            foreach (var aiRes in matchedResults)
                            {
                                // 添加不重复矩形框
                                ParseCommon.AddRect(addedRectKeys, aiRes.Box, true, ref nodeResult);

                            }
                        }
                    }
                    else {
                        if (count > 0)
                        {
                            foreach (var aiRes in matchedResults)
                            {
                                // 添加不重复矩形框
                                ParseCommon.AddRect(addedRectKeys, aiRes.Box, param.IsAutoStudy ? true : isOk, ref nodeResult);

                            }
                        }
                    }
                   

                    #endregion

                    #endregion

                    #region 添加文本信息

                    var isEnable = ParseCommon.GetItemEnableStatus(deteItems, itemName);
                    if (isEnable && !texts.Any(t => t.Text.StartsWith($"{ParseCommon.GetDisplayName(itemName)}:")))
                    {
                        texts.Add(new ColorText(
                            $"{ParseCommon.GetDisplayName(itemName)}:{(isOk ? "OK" : "NG")}",
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

                        // 是否需要转成物理值
                        if (param.NeedConvert)
                        {
                            value = value * param.Scale;
                            rawValue = value.ToString("F0"); // 保留两位小数
                        }



                        bool minOk = float.TryParse(item.MinValue, out float min) && value >= min;
                        bool maxOk = float.TryParse(item.MaxValue, out float max) && value <= max;
                        bool isOk = minOk && maxOk;

                        // 一旦有一个 false，则整体不是 OK
                        if (!isOk)
                            allOk = false;

                        var singleResult = new SingleDetectResult
                        {
                            Name = itemName,
                            Value = rawValue,
                            IsOk = param.IsAutoStudy ? true : isOk // 如果是自动学习模式，则结果默认标记为OK
                        };
                        valueStr += (value.ToString("F0") + ","); // 拼接所有值
                        singleResults.Add(singleResult);

                        // 添加自动学习数据
                        if (param.IsAutoStudy)
                        {
                            if (!_autoStudyDatas.ContainsKey(itemName))
                                _autoStudyDatas.Add(itemName, new List<float>());
                            _autoStudyDatas[itemName].Add(value);
                        }

                        // 添加不重复矩形框
                        ParseCommon.AddRect(addedRectKeys, result.Box, param.IsAutoStudy ? true : isOk, ref nodeResult);

                        hasData = true;
                    }

                    if (matchedResults.Count > 0)
                    {
                        // -------- 添加文本显示 --------0
                        texts.Add(new ColorText(
                            $"{ParseCommon.GetDisplayName(itemName)}: {(allOk ? "OK" : "NG")} ",
                            (param.IsAutoStudy ? true : allOk) ? Color.Green : Color.Red
                        ));

                        // 设置当前检测项的值到结果中
                        ParseCommon.SetDetectItemCurValue(ref param, itemName, (matchedResults.Count > 0 ? ProcessString(valueStr) : "0"));
                    }
                }

                #endregion


                #region 特殊情况处理：检测项已启用，但模型没有检出一个该项的 ObbResult 结果

                // 对于数量型检测项，在正常流程中已处理了这种情况（valueStr = "-1", isOk = false）
                // 所以这里只需处理非数量型检测项中的特殊案+例

                if (!matchedResults.Any() && item.Enable && !isCountItem)
                {
                    string defaultValue = null;
                    bool isSpecialCaseOk = false;

                    // 对于“存在”即NG的检测项要这样设置
                    //if (itemName == "剥口偏移")
                    //{
                    //    defaultValue = "0";
                    //    isSpecialCaseOk = true;
                    //}

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

            //写入当前结果
            bool allOk2 = detectResults.Values
                .SelectMany(list => list)     // 展开所有 List
                .All(r => r.IsOk);            // 只要有一个 false 就返回 false


            //if (allOk2)
            //{
            //    process.OKNumber++;
            //}
            //else process.NGNumber++;

            //texts.Add(new ColorText(
            //       $"当前产品总数量: {process.OKNumber + process.NGNumber}",
            //       Color.Green));

            //texts.Add(new ColorText(
            //       $"当前良品总数量: {process.OKNumber}",
            //       Color.Green));
            //texts.Add(new ColorText(
            //       $"当前良品率: {((process.OKNumber * 1.0) / (process.OKNumber + process.NGNumber) * 100).ToString("F2")}",
            //       Color.Green));

            //texts.Add(new ColorText(
            //       $"当前产品结果: {(allOk2 ? "OK" : "NG")}",
            //       allOk2 ? Color.Green : Color.Red));

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

        public static string ProcessString(string input)
        {
            // 以逗号分割字符串，移除空条目
            string[] parts = input.Split(new char[] { ','}, StringSplitOptions.RemoveEmptyEntries);

            // 过滤并去除空格
            var filteredParts = parts.Select(p => p.Trim())
                                     .Where(p => !string.IsNullOrEmpty(p))
                                     .ToArray();

            if (filteredParts.Length == 0)
            {
                // 如果没有有效数据，返回空字符串或其他默认值
                return string.Empty;
            }
            else if (filteredParts.Length == 1)
            {
                // 只有一个值时，返回该值
                return filteredParts[0];
            }
            else
            {
                // 有多个值时，找最小值和最大值
                // 尝试作为数字比较
                if (decimal.TryParse(filteredParts[0], out _))
                {
                    try
                    {
                        decimal[] numbers = filteredParts.Select(decimal.Parse).ToArray();
                        decimal min = numbers.Min();
                        decimal max = numbers.Max();

                        // 如果最小值和最大值相等，只返回一个值
                        if (min == max)
                        {
                            return min.ToString();
                        }
                        return $"{min},{max}";
                    }
                    catch
                    {
                        // 如果转换失败，按字符串处理
                        string min = filteredParts.Min();
                        string max = filteredParts.Max();

                        if (min == max)
                        {
                            return min;
                        }
                        return $"{min},{max}";
                    }
                }
                else
                {
                    // 按字符串处理
                    string min = filteredParts.Min();
                    string max = filteredParts.Max();

                    if (min == max)
                    {
                        return min;
                    }
                    return $"{min},{max}";
                }
            }
        }

        /// <summary>
        /// 分离离群矩形框
        /// </summary>
        /// <param name="results">检测结果列表</param>
        /// <param name="distance">欧几里得距离</param>
        /// <returns>(非离群Rect列表, 离群Rect列表)</returns>
        public static (List<Rect>, List<Rect>) SeparateOutlierRects(
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

                // 1. 提取每个 Rect 的中心点
                var centers = results.Select(r =>
                {
                    var box = r.Box;
                    float cx = box.X + box.Width / 2.0f;
                    float cy = box.Y + box.Height / 2.0f;
                    return new PointF(cx, cy);
                }).ToList();

                // 2. 计算所有中心点的质心
                float centerX = centers.Average(p => p.X);
                float centerY = centers.Average(p => p.Y);

                // 3. 计算每个中心点到质心的欧几里得距离
                var distances = centers.Select(p =>
                {
                    double dx = p.X - centerX;
                    double dy = p.Y - centerY;
                    return Math.Sqrt(dx * dx + dy * dy);
                }).ToList();

                // 4. 分离非离群和离群的 Rect
                for (int i = 0; i < results.Count; i++)
                {
                    bool isOk = false;
                    var rawValue = distances[i].ToString("F0");
                    // 是否需要转成物理值
                    if (param.NeedConvert)
                    {
                        distances[i] = distances[i] * param.Scale;
                        rawValue = distances[i].ToString("F0"); // 保留两位小数
                    }

                    // 添加自动学习数据
                    if (param.IsAutoStudy)
                    {
                        if (!_autoStudyDatas.ContainsKey(itemName))
                            _autoStudyDatas.Add(itemName, new List<float>());
                        _autoStudyDatas[itemName].Add(float.Parse(rawValue));
                    }

                    if (distances[i] <= distance)
                    {
                        inliers.Add(results[i].Box);
                        isOk = true; // 在距离范围内，标记为OK
                    }
                    else
                        outliers.Add(results[i].Box);
                    // 添加单个检测结果
                    var singleResult = new SingleDetectResult
                    {
                        Name = itemName,
                        Value = rawValue,
                        IsOk = param.IsAutoStudy ? true : isOk // 如果是自动学习模式，则结果默认标记为OK
                    };
                    resStr += (rawValue + ",");  // 拼接所有值
                    singleDetectResults.Add(singleResult);

                }
            }
            catch (Exception e)
            {
                LogHelper.AddLog(MsgLevel.Exception, e.Message, true);
            }

            return (inliers, outliers);
        }
    }
}
