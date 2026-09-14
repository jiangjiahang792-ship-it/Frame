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
    /// 超声波焊接模型结果解析类
    /// </summary>
    public class Ultrasound_Side_3Class
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

            // 用来保存检测结果的检测项OK/NG、带颜色的框、带颜色的文本
            var detectResults = new Dictionary<string, List<SingleDetectResult>>();
            var texts = new List<ColorText>();
            HashSet<string> addedRectKeys = new HashSet<string>(); // 非数量型仍需去重
            // 咬胶占比
            float rubber_left = 0f;
            float rubber_right = 0f;
            float line_right = 0f;
            Rect rect = new Rect();

            // 预计算 class_id=0 的左/右线芯（按中心X排序）
            DetResult? leftCore = null;
            DetResult? rightCore = null;
            if (results != null)
            {
                var class0List = results.Where(r => r.ClassId == 0).OrderBy(r => r.Box.X + r.Box.Width / 2.0).ToList();
                if (class0List.Count >= 1)
                {
                    leftCore = class0List.First();
                    if (class0List.Count >= 2)
                        rightCore = class0List.Last();
                }
            }

            int? leftCenterX2 = leftCore.HasValue ? (int?)(leftCore.Value.Box.X * 2 + leftCore.Value.Box.Width) : null;
            int? rightCenterX2 = rightCore.HasValue ? (int?)(rightCore.Value.Box.X * 2 + rightCore.Value.Box.Width) : null;


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
                    case "左线芯长度":
                        extractValueFunc = r => {
                            if (r.ClassId != 0) return null;
                            if (!leftCenterX2.HasValue) return null;
                            int centerX2 = r.Box.X * 2 + r.Box.Width;
                            return centerX2 == leftCenterX2.Value ? r.Box.Width.ToString("F2") : null;
                        };
                        break;
                    case "左线芯宽度":
                        extractValueFunc = r => {
                            if (r.ClassId != 0) return null;
                            if (!leftCenterX2.HasValue) return null;
                            int centerX2 = r.Box.X * 2 + r.Box.Width;
                            return centerX2 == leftCenterX2.Value ? r.Box.Height.ToString("F2") : null;
                        };
                        break;
                    case "焊接区域长度":
                        extractValueFunc = r => r.ClassId == 1 ? r.Box.Width.ToString("F2") : null;
                        break;
                    case "焊接区域宽度":
                        extractValueFunc = r => r.ClassId == 1 ? r.Box.Height.ToString("F2") : null;
                        break;
                    case "右线芯长度":
                        extractValueFunc = r => {
                            if (r.ClassId != 0) return null;
                            if (!rightCenterX2.HasValue) return null;
                            int centerX2 = r.Box.X * 2 + r.Box.Width;
                            return centerX2 == rightCenterX2.Value ? r.Box.Width.ToString("F2") : null;
                        };
                        break;
                    case "右线芯宽度":
                        extractValueFunc = r => {
                            if (r.ClassId != 0) return null;
                            if (!rightCenterX2.HasValue) return null;
                            int centerX2 = r.Box.X * 2 + r.Box.Width;
                            return centerX2 == rightCenterX2.Value ? r.Box.Height.ToString("F2") : null;
                        };
                        break;
                    case "飞丝":
                        extractValueFunc = r => r.ClassId == 2 ? "1" : null;
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
                        "飞丝",
                        "飞胶",
                        "防水栓反插",
                        "防水栓破损"
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
                    if (isEnable && !texts.Any(t => t.Text.StartsWith($"{itemName}:")))
                    {
                        texts.Add(new ColorText(
                            $"{itemName}:{valueStr}",
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

                    //if (itemName == "红蓝线序")
                    //{
                    //    // 获取所有 ClassId == 4 的检测结果
                    //    var classId4Results = results.Where(r => r.ClassId == 4).ToList();

                    //    // 用于存储颜色识别结果
                    //    var colorRecognitionResults = new List<(DetResult detResult, Rect colorRect, string colorName, double score)>();

                    //    // 获取 score 区间判断的上下限
                    //    bool hasMinScore = float.TryParse(item.MinValue, out float minScore);
                    //    bool hasMaxScore = float.TryParse(item.MaxValue, out float maxScore);

                    //    // 对每个 class_id=4 的结果进行颜色识别
                    //    foreach (var result in classId4Results)
                    //    {
                    //        // 获取原始矩形框
                    //        Rect originalRect = result.Box;

                    //        // 创建相同大小的矩形框，位置位于原矩形框下方
                    //        // Y 坐标 = 原矩形的底部 Y 坐标（原矩形 Y + Height）
                    //        Rect newRect = new Rect(
                    //            originalRect.X,                                    // X 坐标保持不变
                    //            originalRect.Y + originalRect.Height,              // Y 坐标 = 原矩形底部
                    //            originalRect.Width,                                 // 宽度相同
                    //            originalRect.Height                                 // 高度相同
                    //        );

                    //        // 如果提供了源图像，则进行颜色识别
                    //        if (sourceImage != null && !sourceImage.Empty())
                    //        {
                    //            // 确保矩形框在图像范围内
                    //            Rect imageRect = new Rect(0, 0, sourceImage.Width, sourceImage.Height);
                    //            Rect validRect = newRect.Intersect(imageRect);

                    //            if (validRect.Width > 0 && validRect.Height > 0)
                    //            {
                    //                // 提取矩形区域
                    //                Mat roi = new Mat(sourceImage, validRect);

                    //                // 创建颜色分类器并识别颜色
                    //                ColorClassify colorClassifier = new ColorClassify(debug: false);
                    //                var colorResults = colorClassifier.Classify(roi);

                    //                // 获取最佳匹配的颜色和分数
                    //                if (colorResults != null && colorResults.Count > 0)
                    //                {
                    //                    var bestMatch = colorResults[0];
                    //                    string colorName = bestMatch.Key;
                    //                    double score = bestMatch.Value;

                    //                    // 区间判定：MinValue <= score <= MaxValue
                    //                    bool scoreOk = true;
                    //                    if (hasMinScore && score < minScore)
                    //                        scoreOk = false;
                    //                    if (hasMaxScore && score > maxScore)
                    //                        scoreOk = false;

                    //                    // 只有符合区间的颜色才保存
                    //                    if (scoreOk)
                    //                    {
                    //                        colorRecognitionResults.Add((result, newRect, colorName, score));
                    //                    }
                    //                }

                    //                roi.Dispose();
                    //            }
                    //        }
                    //    }

                    //    // 按 X 坐标从左到右排序
                    //    var sortedResults = colorRecognitionResults.OrderBy(r => r.detResult.Box.X).ToList();

                    //    // 检查颜色顺序：左边应该是红色，右边应该是蓝色
                    //    bool seqOk = false;
                    //    string valueText = "";

                    //    if (sortedResults.Count >= 2)
                    //    {
                    //        var leftResult = sortedResults[0];
                    //        var rightResult = sortedResults[sortedResults.Count - 1];

                    //        // 左边应该是红色，右边应该是蓝色
                    //        bool leftIsRed = leftResult.colorName.Contains("红");
                    //        bool rightIsBlue = rightResult.colorName.Contains("蓝");
                    //        seqOk = leftIsRed && rightIsBlue;

                    //        valueText = string.Join(",", sortedResults.Select(r => $"{r.colorName}:{r.score:F2}"));
                    //    }
                    //    else if (sortedResults.Count == 1)
                    //    {
                    //        // 只有一个结果时，无法判断顺序，标记为 NG
                    //        seqOk = false;
                    //        valueText = $"{sortedResults[0].colorName}:{sortedResults[0].score:F2}";
                    //    }
                    //    else
                    //    {
                    //        // 没有符合区间的颜色识别结果
                    //        seqOk = false;
                    //        valueText = "无符合区间的颜色识别结果";
                    //    }

                    //    // 添加矩形框，根据 seqOk 设置颜色
                    //    foreach (var colorResult in sortedResults)
                    //    {
                    //        // 调整中心点 Y 坐标向下移动1
                    //        Rect adjustedRect = AdjustRectCenterY(colorResult.colorRect, -1, sourceImage?.Width ?? 0, sourceImage?.Height ?? 0);

                    //        // 根据 seqOk 设置矩形框颜色：true=绿色，false=红色
                    //        ParseCommon.AddRect(addedRectKeys, adjustedRect, seqOk, ref nodeResult);
                    //    }

                    //    // 如果没有符合区间的结果，也要显示矩形框（红色）
                    //    if (sortedResults.Count == 0)
                    //    {
                    //        foreach (var result in classId4Results)
                    //        {
                    //            Rect originalRect = result.Box;
                    //            Rect newRect = new Rect(
                    //                originalRect.X,
                    //                originalRect.Y + originalRect.Height,
                    //                originalRect.Width,
                    //                originalRect.Height
                    //            );

                    //            // 调整中心点 Y 坐标向下移动1
                    //            Rect adjustedRect = AdjustRectCenterY(newRect, -1, sourceImage?.Width ?? 0, sourceImage?.Height ?? 0);

                    //            // 没有符合区间的结果，显示为红色
                    //            ParseCommon.AddRect(addedRectKeys, adjustedRect, false, ref nodeResult);
                    //        }
                    //    }

                    //    // 添加检测结果
                    //    var rbResult = new SingleDetectResult
                    //    {
                    //        Name = itemName,
                    //        Value = valueText,
                    //        IsOk = param.IsAutoStudy ? true : seqOk
                    //    };
                    //    singleResults.Add(rbResult);
                    //    valueStr = valueText;
                    //    allOk = seqOk;

                    //    // 添加文本信息（只有符合区间的颜色才输出 colorName）
                    //    if (sortedResults.Count > 0)
                    //    {
                    //        texts.Add(new ColorText(
                    //            $"{itemName}: {((param.IsAutoStudy ? true : seqOk) ? "OK" : "NG")} 值:{valueText}",
                    //            (param.IsAutoStudy ? true : seqOk) ? Color.Green : Color.Red
                    //        ));
                    //    }
                    //    else
                    //    {
                    //        texts.Add(new ColorText(
                    //            $"{itemName}: NG 值:{valueText}",
                    //            Color.Red
                    //        ));
                    //    }

                    //    ParseCommon.SetDetectItemCurValue(ref param, itemName, valueText);
                    //    hasData = true;
                    //}
                    //else
                    //{

                    //无匹配结果
                    //正常匹配结果处理
                    //}
                    #endregion
                    //无匹配结果
                    if (!matchedResults.Any())
                    {
                        string defaultValue = "-1";
                        bool isSpecialCaseOk = false;


                        //对于“存在”即NG的检测项要这样设置
                        if (itemName == "飞丝")
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
                            if (isEnable && !texts.Any(t => t.Text.StartsWith($"{itemName}:")))
                            {
                                texts.Add(new ColorText(
                                    $"{itemName}:{defaultValue}",
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

                            if (itemName == "咬胶占比")
                            {
                                rubber_left = result.Box.Left;
                                rubber_right = result.Box.Right;
                                rect = result.Box;
                                continue;
                            }
                            // 保存"咬胶占比"所需的数据
                            if (itemName == "后铆脚露线芯长度")
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

                        // 不是"咬胶占比"时
                        if (itemName != "咬胶占比")
                        {
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
                            if (isEnable && !texts.Any(t => t.Text.StartsWith($"{itemName}:")))
                            {
                                texts.Add(new ColorText(
                                    $"{itemName}:{displayValueStr}",
                                    (param.IsAutoStudy ? true : allOk) ? Color.Green : Color.Red
                                ));
                            }

                            // 设置当前检测项的值到结果中
                            ParseCommon.SetDetectItemCurValue(ref param, itemName, valueStr.TrimEnd(','));
                        }
                    }

                }

                #endregion
                // 添加一个检测项的结果数据
                if (hasData)
                {
                    detectResults[itemName] = singleResults;
                }
            }

            #region 单独处理“咬胶占比”结果
            string name = "咬胶占比";
            var resultList = new List<SingleDetectResult>();
            var res = new SingleDetectResult();
            res.Name = name;
            var enable = ParseCommon.GetItemEnableStatus(deteItems, name);
            if (enable)
            {
                float valueF = ((rubber_right - rubber_left) / (line_right - rubber_left)) * 100f;
                res.Value = valueF.ToString("F2");
                var itemInfo = deteItems.Find(i => i.Name == name);
                float minF = float.Parse(itemInfo.MinValue);
                float maxF = float.Parse(itemInfo.MaxValue);
                res.IsOk = (valueF >= minF && valueF <= maxF);
                resultList.Add(res);
                detectResults[name] = resultList;

                // 添加自动学习数据
                if (param.IsAutoStudy)
                {
                    if (!_autoStudyDatas.ContainsKey(name))
                        _autoStudyDatas.Add(name, new List<float>());
                    _autoStudyDatas[name].Add(valueF);
                }

                // 添加咬胶矩形框
                ParseCommon.AddRect(addedRectKeys, rect, res.IsOk, ref nodeResult);
                // 遍历完成后统一添加文本信息
                if (enable && !texts.Any(t => t.Text.StartsWith(name)))
                {
                    texts.Add(new ColorText(
                        $"{name}:{res.Value}",
                        res.IsOk ? Color.Green : Color.Red
                    ));
                }

                // 设置当前检测项的值到结果中
                ParseCommon.SetDetectItemCurValue(ref param, name, res.Value);
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
