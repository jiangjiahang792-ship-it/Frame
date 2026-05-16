using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Text;
using OpenCvSharp;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

namespace TDJS_Vision.Node._3_Detection.TDAI.Parse
{
    /// <summary>
    /// 5类亿方舟注塑前结果解析类
    /// </summary>
    public class YFZ_Filling_rubber_Front
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
        ///  5类亿方舟注塑前结果解析
        /// </summary>
        /// <param name="results"></param>
        /// <param name="resultCount"></param>
        /// <param name="nodeResult"></param>
        public static void Parse(List<DetResult> results, int resultCount, NodeParamTDAI param, ref NodeResultTDAI nodeResult)
        {
            // 当前使用的检测项配置
            var deteItems = Solution.Instance.DetectItemDic[param.CurDetectItemName];
            ParseCommon.NormalizeDetectItems(deteItems);

            // 用来保存检测结果的检测项OK/NG、带颜色的框、带颜色的文本
            var detectResults = new Dictionary<string, List<SingleDetectResult>>();
            // 间距结果
            List<DetResult> shrapnelRes = new List<DetResult>();
            var texts = new List<ColorText>();
            HashSet<string> addedRectKeys = new HashSet<string>(); // 非数量型仍需去重
            // 咬胶占比
            float rubber_left = 0f;
            float rubber_right = 0f;
            float line_right = 0f;
            Rect rect = new Rect();

            foreach (var item in deteItems)
            {
                string itemName = item.Name;
                // 检测项没启用就不用处理
                if (!item.Enable) continue;

                bool hasData = false;

                #region 提取函数定义，某个检测项的值取自哪个class_id结果的什么属性？

                Func<DetResult, string> extractValueFunc = null;

                switch (itemName)
                {
                    case "DetectItem.Positioning":
                        extractValueFunc = r => r.ClassId == 0 ? "1": null;
                        break;
                    case "DetectItem.TerminalCount":
                        extractValueFunc = r => r.ClassId == 1 ? "1" : null;
                        break;
                    case "DetectItem.Spacing":
                        extractValueFunc = r => r.ClassId == 1 ? "1" : null;
                        break;
                    case "DetectItem.WireBent":
                        extractValueFunc = r => r.ClassId == 2 ?"1" : null;
                        break;
                    case "DetectItem.WireInSlot":
                        extractValueFunc = r => r.ClassId == 3 ? "1" : null;
                        break;
                    case "DetectItem.WireOutOfSlot":
                        extractValueFunc = r => r.ClassId == 4 ? "1" : null;
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

                var singleResults = new List<SingleDetectResult>();

                #region 正常情况: 检测项已启用，模型有检出该项的DetResult结果

                // 获取当前检测项是否为“数量型”
                bool isCountItem = item.IsCountItem;

                if (isCountItem)
                {
                    // 数量型检测项：统计总数，只添加一次结果
                    int count = matchedResults.Count;
                    string valueStr = count.ToString();
                    bool isOk = false;
                    bool hasClass4 = results != null && results.Any(r => r.ClassId == 4);
                    // 添加自动学习数据
                    if (param.IsAutoStudy)
                    {
                        if (!_autoStudyDatas.ContainsKey(itemName))
                            _autoStudyDatas.Add(itemName, new List<float>());
                        _autoStudyDatas[itemName].Add(count);
                    }

                    List<string> ngCountItems = new List<string>()
                    {
                        "DetectItem.WireOutOfSlot",
                        "DetectItem.WireBent"

                    };

                    #region 判断检测项是否OK

                    if (count > 0)
                    {
                        // 有结果时才判断上下限
                        bool minOk = float.TryParse(item.MinValue, out float min) && count >= min;
                        bool maxOk = float.TryParse(item.MaxValue, out float max) && count <= max;
                        isOk = minOk && maxOk;

                        // 规则覆盖：当存在“线不在槽”(class_id=5)时，“线在槽”(class_id=4)的框必须为绿色
                        if (itemName == "DetectItem.WireInSlot" && hasClass4)
                            isOk = true;

                        // 规则覆盖：当“线不在槽”数量>0时，其框必须为红色
                        if (itemName == "DetectItem.WireOutOfSlot" && count > 0)
                            isOk = false;
                    }
                    else
                    {
                        // 没有结果时，默认值 0，直接标记为 ok
                        valueStr = "0";
                        isOk = ngCountItems.Contains(itemName) ? true : false; // NG型检测项没有结果时标记为OK
                    }

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
                    #region 添加矩形框

                    if (count > 0)
                    {
                        foreach (var aiRes in matchedResults)
                        {
                            // “线在槽”在存在 class_id=5 时绿框
                            if (itemName == "DetectItem.WireInSlot" && results != null && results.Any(r => r.ClassId == 4))
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

                    if (itemName == "DetectItem.Spacing")
                    {
                        shrapnelRes = matchedResults;
                        continue;
                    }
                    foreach (var result in matchedResults)
                    {
                        string rawValue = extractValueFunc(result);
                        if (!float.TryParse(rawValue, out float value))
                            continue;

                        // 是否需要转成物理值
                        if (param.NeedConvert)
                        {
                            value = value * param.Scale;
                            rawValue = value.ToString("F2"); // 保留两位小数
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
                        valueStr += (rawValue + ","); // 拼接所有值
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

                    foreach (var result in matchedResults)
                    {
                        string rawValue = extractValueFunc(result);
                        if (!float.TryParse(rawValue, out float value))
                            continue;

                        // 是否需要转成物理值
                        if (param.NeedConvert)
                        {
                            value = value * param.Scale;
                            rawValue = value.ToString("F2"); // 保留两位小数
                        }

                        if (itemName == "DetectItem.BiteGlueRatio")
                        {
                            rubber_left = result.Box.Left;
                            rubber_right = result.Box.Right;
                            rect = result.Box;
                            continue;
                        }
                        // 保存“咬胶占比”所需的数据
                        if (itemName == "DetectItem.BackRivetCoreLength")
                            line_right = result.Box.Right;

                        


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
                        valueStr += (rawValue + ","); // 拼接所有值
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

                    // 不是“咬胶占比”时
                    if (itemName != "DetectItem.BiteGlueRatio")
                    {
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
                    }
                }

                #endregion


                #region 特殊情况处理：检测项已启用，但模型没有检出一个该项的DetResult结果

                // 对于数量型检测项，在正常流程中已处理了这种情况（valueStr = "-1", isOk = false）
                // 所以这里只需处理非数量型检测项中的特殊案例："不良焊锡面积"

                if (!matchedResults.Any() && item.Enable && !isCountItem)
                {
                    string defaultValue = null;
                    bool isSpecialCaseOk = false;


                    //对于“存在”即NG的检测项要这样设置
                    if (itemName == "DetectItem.WireOutOfSlot"|| itemName == "DetectItem.WireBent")
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

                        // 添加文本
                        var isEnable = ParseCommon.GetItemEnableStatus(deteItems, itemName);
                        if (isEnable && !texts.Any(t => t.Text.StartsWith($"{ParseCommon.GetDisplayName(itemName)}:")))
                        {
                            texts.Add(new ColorText(
                                $"{ParseCommon.GetDisplayName(itemName)}: {(isSpecialCaseOk ? "OK" : "NG")}",
                                isSpecialCaseOk ? Color.Green : Color.Red
                            ));
                        }

                        hasData = true;
                    }

                    #endregion
                }



                // 添加一个检测项的结果数据
                if (hasData)
                {
                    detectResults[itemName] = singleResults;
                }
            }          

            #region 单独处理“咬胶占比”结果


            //string name = "DetectItem.BiteGlueRatio";
            //var resultList = new List<SingleDetectResult>();
            //var res = new SingleDetectResult();
            //res.Name = name;
            //var enable = ParseCommon.GetItemEnableStatus(param, name);
            //float valueF = ((rubber_right - rubber_left) / (line_right - rubber_left)) * 100f;
            //res.Value = valueF.ToString("F2");
            //var itemInfo = param.AIInputInfo.DetectItems.Find(i => i.Name == name);
            //float minF = float.Parse(itemInfo.MinValue);
            //float maxF = float.Parse(itemInfo.MaxValue);
            //res.IsOk = (valueF >= minF && valueF <= maxF);
            //resultList.Add(res);
            //detectResults[name] = resultList;

            //// 添加自动学习数据
            //if (param.IsAutoStudy)
            //{
            //    if (!_autoStudyDatas.ContainsKey(name))
            //        _autoStudyDatas.Add(name, new List<float>());
            //    _autoStudyDatas[name].Add(valueF);
            //}

            //// 添加咬胶矩形框
            //ParseCommon.AddRect(addedRectKeys, rect, res.IsOk, ref nodeResult);
            //// 遍历完成后统一添加文本信息
            //if (enable && !texts.Any(t => t.Text.StartsWith(name)))
            //{
            //    texts.Add(new ColorText(
            //        $"{name}: {(res.IsOk ? "OK" : "NG")}",
            //        res.IsOk ? Color.Green : Color.Red
            //    ));
            //}

            //// 设置当前检测项的值到结果中
            //ParseCommon.SetDetectItemCurValue(ref param, name, res.Value);
            #endregion
            #region 单独处理“间距”结果

            var resultList = new List<SingleDetectResult>();
            string valueRes = string.Empty;
            var enable = ParseCommon.GetItemEnableStatus(deteItems, "DetectItem.Spacing");
            var itemInfo = deteItems.Find(i => i.Name == "DetectItem.Spacing");

            if (enable)
            {
                if (itemInfo != null)
                {
                    float.TryParse(itemInfo.MinValue, out float minI);
                    float.TryParse(itemInfo.MaxValue, out float maxI);

                    // 分别取 class 0 与 class 1，按 X 从左到右成对
                    var class0ListAll = results?.Where(r => r.ClassId == 0).OrderBy(r => r.Box.X).ToList() ?? new List<DetResult>();
                    var class1ListAll = results?.Where(r => r.ClassId == 1).OrderBy(r => r.Box.X).ToList() ?? new List<DetResult>();
                    int pairCount = Math.Min(class0ListAll.Count, class1ListAll.Count);

                    bool allOk = true;
                    for (int i = 0; i < pairCount; i++)
                    {
                        var b0 = class0ListAll[i].Box; // 下方：取底边
                        var b1 = class1ListAll[i].Box; // 上方：取顶边
                        float y0 = b0.Y + b0.Height;   // class 0 底边 Y
                        float y1 = b1.Y;               // class 1 顶边 Y
                        float d = y1 - y0;
                        if (param.NeedConvert) d = d * param.Scale;

                        var raw = d.ToString("F2");
                        valueRes += raw + ",";

                        bool minOk = d >= minI;
                        bool maxOk = d <= maxI;
                        bool isOk = minOk && maxOk;
                        if (!isOk) allOk = false;

                        resultList.Add(new SingleDetectResult
                        {
                            Name = "DetectItem.Spacing",
                            Value = raw,
                            IsOk = param.IsAutoStudy ? true : isOk
                        });

                        // 矩形框显示：同时标注上下两个框
                        ParseCommon.AddRect(addedRectKeys, b0, param.IsAutoStudy ? true : isOk, ref nodeResult);
                        ParseCommon.AddRect(addedRectKeys, b1, param.IsAutoStudy ? true : isOk, ref nodeResult);

                        // 自动学习
                        if (param.IsAutoStudy)
                        {
                            if (!_autoStudyDatas.ContainsKey("DetectItem.Spacing"))
                                _autoStudyDatas.Add("DetectItem.Spacing", new List<float>());
                            _autoStudyDatas["DetectItem.Spacing"].Add(d);
                        }
                    }

                    // 汇总文本

                    if (enable && !texts.Any(t => t.Text.StartsWith($"{ParseCommon.GetDisplayName("DetectItem.Spacing")}:")))
                    {
                        texts.Add(new ColorText(
                            $"{ParseCommon.GetDisplayName("DetectItem.Spacing")}: {(param.IsAutoStudy ? "OK" : (allOk ? "OK" : "NG"))}",
                            (param.IsAutoStudy ? true : allOk) ? Color.Green : Color.Red
                        ));
                    }

                    // 设置当前检测项的值到结果中
                    ParseCommon.SetDetectItemCurValue(ref param, "DetectItem.Spacing", valueRes.TrimEnd(','));
                    detectResults["DetectItem.Spacing"] = resultList;
                }

            }

            #endregion

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
        /// 检查所有上下距离是否小于设定的距离
        /// </summary>
        /// <param name="detResults">弹片结果</param>
        /// <param name="distance">设定的距离</param>
        /// <param name="delta">x坐标在偏差delta范围内判定为上下一对弹片</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        private static List<bool> ValidateRectPairs(List<DetResult> detResults, float dMin, float dMax, NodeParamTDAI param, int delta, HashSet<string> addedRectKeys, NodeResultTDAI nodeResult, ref string value)
        {
            if (detResults == null || detResults.Count % 2 != 0)
                throw new ArgumentException("端子 检测结果列表必须包含偶数个元素。");

            int n = detResults.Count;
            bool[] used = new bool[n];  // 标记每个矩形是否已被配对
            int pairCount = 0;
            List<bool> bools = new List<bool>(); // 保存每个弹片距离是否OK结果

            for (int i = 0; i < n; i++)
            {
                if (used[i]) continue;

                var rectA = detResults[i].Box;

                for (int j = i + 1; j < n; j++)
                {
                    if (used[j]) continue;

                    var rectB = detResults[j].Box;

                    // 检查是否属于同一列（X方向差值在delta范围内）
                    if (Math.Abs(rectA.X - rectB.X) <= delta)
                    {
                        // 确定上下关系
                        Rect topRect = rectA.Y < rectB.Y ? rectA : rectB;
                        Rect bottomRect = rectA.Y < rectB.Y ? rectB : rectA;

                        // 计算间距
                        float d = bottomRect.Top - topRect.Bottom;

                        // 是否需要转成物理值
                        if (param.NeedConvert)
                            d = d * param.Scale;

                        // 添加自动学习数据
                        if (param.IsAutoStudy)
                        {
                            if (!_autoStudyDatas.ContainsKey("DetectItem.BiteGlueRatio"))
                                _autoStudyDatas.Add("DetectItem.BiteGlueRatio", new List<float>());
                            _autoStudyDatas["DetectItem.BiteGlueRatio"].Add(d);
                        }

                        value += d.ToString("F2") + ",";

                        if (d > dMax || d < dMin)
                        {
                            // 添加上下矩形框
                            ParseCommon.AddRect(addedRectKeys, rectA, param.IsAutoStudy ? true : false, ref nodeResult);
                            ParseCommon.AddRect(addedRectKeys, rectB, param.IsAutoStudy ? true : false, ref nodeResult);
                            bools.Add(false);
                        }
                        else
                        {
                            // 添加上下矩形框
                            ParseCommon.AddRect(addedRectKeys, rectA, true, ref nodeResult);
                            ParseCommon.AddRect(addedRectKeys, rectB, true, ref nodeResult);
                            bools.Add(true);
                        }

                        // 标记为已使用
                        used[i] = true;
                        used[j] = true;
                        pairCount++;
                        break;  // 找到配对后跳出内层循环
                    }
                }
            }

            // 检查是否成功找到 n / 2 对
            if (pairCount != n / 2)
            {
                throw new InvalidOperationException("无法正确匹配所有弹片上下对，可能数据不符合要求。");
            }

            return bools;
        }


    }
}
