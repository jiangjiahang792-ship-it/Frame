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
    ///金海自动化焊锡6类结果解析类
    /// </summary>
    public class USB_Tail
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
        ///  12类端子结果解析
        /// </summary>
        /// <param name="results"></param>
        /// <param name="resultCount"></param>
        /// <param name="nodeResult"></param>
        /// <param name="sourceImage">源图像，用于颜色识别（可选）</param>
        public static void Parse(List<DetResult> results, int resultCount, NodeParamTDAI param, ref NodeResultTDAI nodeResult)
        {
            // 当前使用的检测项配置
            var deteItems = Solution.Instance.DetectItemDic[param.CurDetectItemName];
            ParseCommon.NormalizeDetectItems(deteItems);


            // 用来保存检测结果的检测项OK/NG、带颜色的框、带颜色的文本
            var detectResults = new Dictionary<string, List<SingleDetectResult>>();
            var texts = new List<ColorText>();
            HashSet<string> addedRectKeys = new HashSet<string>(); // 非数量型仍需去重
            // 咬胶占比
            Rect rect = new Rect();
            // 预收集：不依赖检测项启用（支持多rubber）
            var rubberRects = new List<Rect>();      // class_id=3
            var front_footRects = new List<Rect>();  // class_id=1
            var behind_footRects = new List<Rect>(); // class_id=4           
            if (results != null && results.Count > 0)
            {
                rubberRects.AddRange(results.Where(r => r.ClassId == 3).Select(r => r.Box));
                front_footRects.AddRange(results.Where(r => r.ClassId == 1).Select(r => r.Box));
                behind_footRects.AddRange(results.Where(r => r.ClassId == 4).Select(r => r.Box));
            }

            // 一开始就检查：启用的检测项是否“缺失”(模型完全没有检出对应class_id)
            // 后续新增检测项只需在 missingRules 里补一行。
            var enabledItemNames = new HashSet<string>(deteItems.Where(d => d.Enable).Select(d => d.Name));
            var classCount = results == null
                ? new Dictionary<int, int>()
                : results.GroupBy(r => r.ClassId).ToDictionary(g => g.Key, g => g.Count());
            bool HasClass(int classId) => classCount.TryGetValue(classId, out int c) && c > 0;

            // item -> 依赖的class_id列表 + 缺失提示
            // 注意：存在即NG/数量型项目不建议放进来（没检出往往是OK）
            const string Missing = "缺失";
            var missingRules = new List<(string itemName, (int classId, string msg)[] deps)>
            {
                ("DetectItem.SolderArea", new []{ (2, Missing) })
                //// 咬胶占比依赖 rubber + 前铆脚 + 后铆脚
                //("DetectItem.BiteGlueRatio",         new []{ (3, "咬胶缺失"), (1, "前铆脚缺失"), (4, "后铆脚缺失") }),
            };
            // 收集缺失并直接写入结果/文本/CurValue，同时记录 missingItemNames，后续循环跳过避免重复输出
            var missingItemNames = new HashSet<string>();
            foreach (var rule in missingRules)
            {
                if (!enabledItemNames.Contains(rule.itemName))
                    continue;

                foreach (var dep in rule.deps)
                {
                    if (HasClass(dep.classId))
                        continue;

                    missingItemNames.Add(rule.itemName);

                    if (!detectResults.ContainsKey(rule.itemName))
                        detectResults[rule.itemName] = new List<SingleDetectResult>();

                    detectResults[rule.itemName].Add(new SingleDetectResult
                    {
                        Name = rule.itemName,
                        Value = dep.msg,
                        IsOk = false
                    });

                    if (!texts.Any(t => t.Text.StartsWith($"{ParseCommon.GetDisplayName(rule.itemName)}:")))
                        texts.Add(new ColorText($"{ParseCommon.GetDisplayName(rule.itemName)}:{dep.msg}", Color.Red));

                    ParseCommon.SetDetectItemCurValue(ref param, rule.itemName, dep.msg);
                }
            }

            foreach (var item in deteItems)
            {
                string itemName = item.Name;
                // 检测项没启用就不用处理
                if (!item.Enable) continue;
                // 已在开头判定缺失的启用项，这里跳过，避免重复输出/重复判定
                if (missingItemNames.Contains(itemName)) continue;

                bool hasData = false;

                #region 提取函数定义，某个检测项的值取自哪个class_id结果的什么属性？

                Func<DetResult, string> extractValueFunc = null;

                switch (itemName)
                {
                    case "DetectItem.Gate":
                        extractValueFunc = r => r.ClassId == 0 ? "1" : null;
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

                    // 添加自动学习数据
                    if (param.IsAutoStudy)
                    {
                        if (!_autoStudyDatas.ContainsKey(itemName))
                            _autoStudyDatas.Add(itemName, new List<float>());
                        _autoStudyDatas[itemName].Add(count);
                    }

                    List<string> ngCountItems = new List<string>()
                    {
                        "DetectItem.PadDamaged",
                        "DetectItem.InsufficientSolder",
                        "DetectItem.FalseSolder",
                        "DetectItem.WireDamaged",

                    };

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

                    #region 添加文本信息

                    var isEnable = ParseCommon.GetItemEnableStatus(deteItems, itemName);
                    if (isEnable && !texts.Any(t => t.Text.StartsWith($"{ParseCommon.GetDisplayName(itemName)}:")))
                    {
                        texts.Add(new ColorText(
                            $"{ParseCommon.GetDisplayName(itemName)}:{valueStr}",
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
                    #region //特殊项处理
                    #endregion
                    //无匹配结果
                    if (!matchedResults.Any())
                    {
                        string defaultValue = Missing;
                        bool isSpecialCaseOk = false;


                        //对于“存在”即NG的检测项要这样设置
                        if (itemName == "DetectItem.InsufficientSolder" || itemName == "DetectItem.FalseSolder" || itemName == "DetectItem.WireDamaged" || itemName == "DetectItem.PadDamaged")
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
                                    $"{ParseCommon.GetDisplayName(itemName)}:{defaultValue}",
                                    isSpecialCaseOk ? Color.Green : Color.Red
                                ));
                            }

                            hasData = true;
                        }
                    }
                    else
                    {
                        //正常匹配结果处理
                        var okValues = new List<string>();
                        var ngValues = new List<string>();
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
                                // rubberRects 已在开头按 class_id=3 预收集，这里不再依赖检测项启用采集
                                rect = result.Box; // 保留用于兼容旧逻辑/显示
                                continue;
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

                            // 分别收集符合和不符合的值（根据实际上下限判断，不受自动学习模式影响）
                            if (isOk)
                            {
                                okValues.Add(rawValue);
                            }
                            else
                            {
                                ngValues.Add(rawValue);
                            }

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
                        // 构建显示值：符合的值只显示第一个，不符合的值全部显示
                        var displayValues = new List<string>();
                        if (okValues.Count > 0)
                        {
                            displayValues.Add(okValues[0]); // 符合的值只显示第一个
                        }
                        displayValues.AddRange(ngValues); // 不符合的值全部显示
                        string displayValueStr = string.Join(",", displayValues);

                        // 遍历完成后统一添加文本信息
                        var isEnable = ParseCommon.GetItemEnableStatus(deteItems, itemName);
                        if (isEnable && !texts.Any(t => t.Text.StartsWith($"{ParseCommon.GetDisplayName(itemName)}:")))
                        {
                            texts.Add(new ColorText(
                                $"{ParseCommon.GetDisplayName(itemName)}:{displayValueStr}",
                                (param.IsAutoStudy ? true : allOk) ? Color.Green : Color.Red
                            ));
                        }

                        // 设置当前检测项的值到结果中
                        ParseCommon.SetDetectItemCurValue(ref param, itemName, valueStr.TrimEnd(','));
                    }

                }

                #endregion
                // 添加一个检测项的结果数据
                if (hasData)
                {
                    detectResults[itemName] = singleResults;
                }
            }

            #region 单独处理“咬胶占比”结果（优化：多rubber，不依赖其他检测项启用，无goto）
            //string name = "DetectItem.BiteGlueRatio";
            //var enable = ParseCommon.GetItemEnableStatus(deteItems, name);
            //if (enable)
            //{
            //    var itemInfo = deteItems.Find(i => i.Name == name);
            //    float minF = float.Parse(itemInfo.MinValue);
            //    float maxF = float.Parse(itemInfo.MaxValue);

            //    var resultList = new List<SingleDetectResult>();
            //    bool allOk = true;

            //    // 缺失情况已在前置 missingRules 里统一处理；这里默认数据齐全，直接计算
            //    {
            //        // 对每个咬胶分别计算：valueF = ((rubber_right - rubber_left) / (front_foot_left - behind_foot_right)) * 100
            //        foreach (var rr in rubberRects)
            //        {
            //            float rubber_left = rr.Left;
            //            float rubber_right = rr.Right;

            //            int rubberCenterX2 = rr.X * 2 + rr.Width;
            //            Rect frontFoot = front_footRects.OrderBy(r => Math.Abs((r.X * 2 + r.Width) - rubberCenterX2)).First();
            //            Rect behindFoot = behind_footRects.OrderBy(r => Math.Abs((r.X * 2 + r.Width) - rubberCenterX2)).First();

            //            float denom = frontFoot.Left - behindFoot.Right;
            //            float numer = rubber_right - rubber_left;

            //            float valueF = -1f;
            //            if (denom > 0 && numer > 0)
            //                valueF = (numer / denom) * 100f;

            //            bool isOk = (valueF >= minF && valueF <= maxF);
            //            if (!isOk) allOk = false;

            //            resultList.Add(new SingleDetectResult
            //            {
            //                Name = name,
            //                Value = valueF.ToString("F2"),
            //                IsOk = param.IsAutoStudy ? true : isOk
            //            });

            //            // 画咬胶框（每个rubber一框）
            //            ParseCommon.AddRect(addedRectKeys, rr, param.IsAutoStudy ? true : isOk, ref nodeResult);

            //            // 自动学习
            //            if (param.IsAutoStudy)
            //            {
            //                if (!_autoStudyDatas.ContainsKey(name))
            //                    _autoStudyDatas.Add(name, new List<float>());
            //                _autoStudyDatas[name].Add(valueF);
            //            }
            //        }
            //    }

            //    detectResults[name] = resultList;

            //    // 文本：显示第一个OK值 + 所有NG值
            //    var okValues = resultList.Where(r => r.IsOk).Select(r => r.Value).ToList();
            //    var ngValues = resultList.Where(r => !r.IsOk).Select(r => r.Value).ToList();
            //    var displayValues = new List<string>();
            //    if (okValues.Count > 0) displayValues.Add(okValues[0]);
            //    displayValues.AddRange(ngValues);
            //    string displayValueStr = string.Join(",", displayValues);

            //    if (enable && !texts.Any(t => t.Text.StartsWith($"{name}:")))
            //    {
            //        texts.Add(new ColorText(
            //            $"{name}:{displayValueStr}",
            //            (param.IsAutoStudy ? true : allOk) ? Color.Green : Color.Red
            //        ));
            //    }

            //    ParseCommon.SetDetectItemCurValue(ref param, name, string.Join(",", resultList.Select(r => r.Value)));
            //}
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
        /// 调整矩形框中心点 Y 坐标（向下移动，Y 值减少）
        /// </summary>
        /// <param name="rect">原始矩形框</param>
        /// <param name="deltaY">Y 坐标变化量（负数表示向下移动，减少 Y 值）</param>
        /// <param name="imgWidth">图像宽度，用于边界检查</param>
        /// <param name="imgHeight">图像高度，用于边界检查</param>
        /// <returns>调整后的矩形框</returns>
        private static Rect AdjustRectCenterY(Rect rect, int deltaY, int imgWidth, int imgHeight)
        {
            if (rect.Width <= 0 || rect.Height <= 0)
                return rect;

            // 计算原始中心点
            int centerX = rect.X + rect.Width / 2;
            int centerY = rect.Y + rect.Height / 2;

            // 调整中心点 Y 坐标（向下移动，减少 Y 值）
            int newCenterY = centerY + deltaY;

            // 计算新的矩形框位置（以新中心点为中心）
            int newX = centerX - rect.Width / 2;
            int newY = newCenterY - rect.Height / 2;

            // 确保矩形框在图像范围内
            if (imgWidth > 0 && imgHeight > 0)
            {
                // 确保 X 坐标在有效范围内
                newX = Math.Max(0, Math.Min(newX, imgWidth - 1));
                // 确保 Y 坐标在有效范围内
                newY = Math.Max(0, Math.Min(newY, imgHeight - 1));

                // 确保矩形框不会超出图像边界
                int maxWidth = imgWidth - newX;
                int maxHeight = imgHeight - newY;

                int newWidth = Math.Min(rect.Width, maxWidth);
                int newHeight = Math.Min(rect.Height, maxHeight);

                if (newWidth <= 0 || newHeight <= 0)
                {
                    System.Diagnostics.Debug.WriteLine($"AdjustRectCenterY: Invalid adjusted rect size. Original: {rect}, deltaY: {deltaY}");
                    return rect; // 如果调整后无效，返回原始矩形框
                }

                return new Rect(newX, newY, newWidth, newHeight);
            }

            return new Rect(newX, newY, rect.Width, rect.Height);
        }

    }
}
