using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Web.UI;
using System.Xml.Linq;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Text;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

namespace TDJS_Vision.Node._3_Detection.TDAI
{
    /// <summary>
    /// AI结果解析类
    /// 作用：将AI检测出来的标签对应结果，解析成客户需要的检测项结果
    /// 比如说，客户需要检测出一款线芯的长度，模型训练有一个line_core标签，
    /// 通过调用模型推理得到这个标签结果，我们要把矩形框的长当做客户
    /// 提的检测项“线芯的长度”的实测值，检测项的OK范围会通过一个配置文件提前定义好
    /// 这个配置文件就是“AI检测节点”参数设置界面要选择的输入文件
    /// </summary>
    internal class AI
    {
        /// <summary>
        /// 解析Type-C检测结果
        /// </summary>
        public static void TypeCResultParse2(List<DetResult> results, int resultCount, ref NodeResultTDAI nodeResult)
        {
            // 如果检测项配置为空或没有启用的检测项，则直接返回，不进行后续处理
            if (nodeResult?.AIInputInfo?.DetectItems == null || !nodeResult.AIInputInfo.DetectItems.Any())
                return;

            // 用来保存检测结果的检测项OK/NG、带颜色的框、带颜色的文本
            var detectResults = new Dictionary<string, List<SingleDetectResult>>();
            var texts = new List<ColorText>();

            foreach (var item in nodeResult.AIInputInfo.DetectItems)
            {
                // 检测项没启用就不用处理
                if (!item.Enable) continue;

                string itemName = item.Name;
                bool hasData = false;

                #region 提取函数定义，某个检测项的值取自哪个class_id结果的什么属性？

                Func<DetResult, string> extractValueFunc = null;

                switch (itemName)
                {
                    case "Type-C数量":
                        extractValueFunc = r => r.ClassId == 0 ? "1" : null;
                        break;
                    case "焊点数量":
                        extractValueFunc = r => r.ClassId == 1 ? "1" : null;
                        break;
                    case "不良焊锡面积":
                        extractValueFunc = r => r.ClassId == 2 ? (r.Box.Width * r.Box.Height).ToString("F2") : null;
                        break;
                    case "露线芯长度":
                        extractValueFunc = r => r.ClassId == 3 ? r.Box.Height.ToString("F2") : null;
                        break;
                    case "黑色线数量":
                        extractValueFunc = r => r.ClassId == 4 ? "1" : null;
                        break;
                    case "红色线数量":
                        extractValueFunc = r => r.ClassId == 5 ? "1" : null;
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

                #region 正常情况: 检测项已启用，模型有检出该项的DetResult结果

                // 获取当前检测项是否为“数量型”
                bool isCountItem = item.IsCountItem;

                if (isCountItem)
                {
                    // 数量型检测项：统计总数，只添加一次结果
                    int count = matchedResults.Count;
                    string valueStr = count.ToString();
                    bool isOk = false;

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
                        // 没有结果时，默认值 -1，直接标记为 NG
                        valueStr = "-1";
                        isOk = false;
                    }

                    #endregion

                    #region 添加检测项结果

                    var singleResult = new SingleDetectResult
                    {
                        Name = itemName,
                        Value = valueStr,
                        IsOk = isOk
                    };
                    singleResults.Add(singleResult);

                    #endregion

                    #region 添加矩形框

                    if (count > 0)
                    {

                        if (nodeResult.AiResult.CurRectsMap.ContainsKey(itemName))
                            nodeResult.AiResult.CurRectsMap[itemName].Clear();
                        else
                            nodeResult.AiResult.CurRectsMap.Add(itemName, new List<ColorRotatedRect>());
                        foreach (var aiRes in matchedResults)
                        {
                            var rect = new ColorRotatedRect(
                                (int)aiRes.Box.X,
                                (int)aiRes.Box.Y,
                                (int)aiRes.Box.Width,
                                (int)aiRes.Box.Height
                            );
                            rect.AddFlag(true);
                            nodeResult.AiResult.CurRectsMap[itemName].Add(rect);

                        }

                        // 当该检测项是OK时，能拿到齐全的框
                        // 改成红色作为齐全的NG框，用于后续绘制
                        if (isOk)
                        {
                            if (nodeResult.AiResult.RectsNgMap.ContainsKey(itemName))
                                nodeResult.AiResult.RectsNgMap[itemName].Clear();
                            else
                                nodeResult.AiResult.RectsNgMap.Add(itemName, new List<ColorRotatedRect>());
                            foreach (var aiRes in matchedResults)
                            {
                                var rectNG = new ColorRotatedRect(
                                    (int)aiRes.Box.X,
                                    (int)aiRes.Box.Y,
                                    (int)aiRes.Box.Width,
                                    (int)aiRes.Box.Height
                                );
                                rectNG.AddFlag(false);
                                nodeResult.AiResult.RectsNgMap[itemName].Add(rectNG);
                            }
                        }
                    }

                    #endregion

                    #region 添加文本信息

                    texts.Add(new ColorText(
                        $"{itemName}: {(isOk ? "OK" : "NG")}",
                        isOk ? Color.Green : Color.Red
                    ));

                    #endregion

                    hasData = true;
                }
                else
                {
                    HashSet<string> addedRectKeys = new HashSet<string>(); // 非数量型仍需去重

                    foreach (var result in matchedResults)
                    {
                        string rawValue = extractValueFunc(result);
                        if (!float.TryParse(rawValue, out float value))
                            continue;

                        bool minOk = float.TryParse(item.MinValue, out float min) && value >= min;
                        bool maxOk = float.TryParse(item.MaxValue, out float max) && value <= max;
                        bool isOk = minOk && maxOk;

                        var singleResult = new SingleDetectResult
                        {
                            Name = itemName,
                            Value = rawValue,
                            IsOk = isOk
                        };
                        singleResults.Add(singleResult);

                        string rectKey = $"{result.Box.X},{result.Box.Y},{result.Box.Width},{result.Box.Height}";

                        // 添加 Rect（如果还没加过）
                        if (!addedRectKeys.Contains(rectKey))
                        {
                            var rect = new ColorRotatedRect(
                                (int)result.Box.X,
                                (int)result.Box.Y,
                                (int)result.Box.Width,
                                (int)result.Box.Height
                            );
                            rect.AddFlag(isOk); // 初始颜色由该检测项决定
                            addedRectKeys.Add(rectKey);

                            if (nodeResult.AiResult.CurRectsMap.ContainsKey(itemName))
                                nodeResult.AiResult.CurRectsMap[itemName].Clear();
                            else
                                nodeResult.AiResult.CurRectsMap.Add(itemName, new List<ColorRotatedRect>());
                            nodeResult.AiResult.CurRectsMap[itemName].Add(rect);

                        }
                        else
                        {
                            // 如果已经加过，则更新颜色（只要有一个 NG 就设为红色）
                            int index = nodeResult.AiResult.CurRectsMap[itemName].FindIndex(r =>
                                r.RotatedRect.center.x == result.Box.X + result.Box.Width / 2 &&
                                r.RotatedRect.center.y == result.Box.Y + result.Box.Height / 2 &&
                                r.RotatedRect.size.width == result.Box.Width &&
                                r.RotatedRect.size.height == result.Box.Height);

                            if (index >= 0)
                            {
                                nodeResult.AiResult.CurRectsMap[itemName][index].AddFlag(isOk); // 更新颜色依据
                            }
                        }

                        // 添加 Text（仅首次出现该检测项时添加，避免重复）
                        if (!texts.Any(t => t.Text.StartsWith($"{itemName}:")))
                        {
                            texts.Add(new ColorText(
                                $"{itemName}: {(isOk ? "OK" : "NG")}",
                                isOk ? Color.Green : Color.Red
                            ));
                        }

                        hasData = true;
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

                    // 对于“存在”即NG的检测项要这样设置
                    if (itemName == "不良焊锡面积")
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
                            $"{itemName}: {(isSpecialCaseOk ? "OK" : "NG")}",
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

            // 更新 AiResult
            nodeResult.AiResult.DetectResults = detectResults;
            //nodeResult.AiResult.Rects = rects;
            nodeResult.AiResult.Texts = texts;
        }

        /// <summary>
        /// 解析线芯检测结果
        /// </summary>
        public static void LineCoreResultParse(List<ObbResult> results, int resultCount, ref NodeResultTDAI nodeResult)
        {
            // 按照class_id分组
            var sortDic = SortOBBResults(results, resultCount);

            #region 获取检测项的上下限
            var lineCoreRange = GetMinMaxValues(nodeResult, "线芯数量");
            var heightRange = GetMinMaxValues(nodeResult, "线芯长度");
            var widthRange = GetMinMaxValues(nodeResult, "线芯宽度");
            var angleRange = GetMinMaxValues(nodeResult, "线芯角度");
            var lineCoreOffsetRange = GetMinMaxValues(nodeResult, "剥口偏移");
            #endregion

            #region 初始化 DetectResults 列表
            nodeResult.AiResult.DetectResults["线芯数量"] = new List<SingleDetectResult>();
            nodeResult.AiResult.DetectResults["线芯长度"] = new List<SingleDetectResult>();
            nodeResult.AiResult.DetectResults["线芯宽度"] = new List<SingleDetectResult>();
            nodeResult.AiResult.DetectResults["线芯角度"] = new List<SingleDetectResult>();
            nodeResult.AiResult.DetectResults["剥口偏移"] = new List<SingleDetectResult>();
            #endregion

            #region 添加“线芯数量”检测结果
            if (sortDic.ContainsKey(0))
            {
                nodeResult.AiResult.DetectResults["线芯数量"].Add(
                    BuildSingleDetectResult(
                        "线芯数量",
                        sortDic[0].Count,
                        GetItemEnableStatus(nodeResult, "线芯数量"),
                        lineCoreRange.min,
                        lineCoreRange.max
                    )
                );
                // 设置“线芯数量”当前值
                SetDetectItemCurValue(ref nodeResult, "线芯数量", sortDic[0].Count.ToString());
            }
            else
            {
                nodeResult.AiResult.DetectResults["线芯数量"].Add(
                    new SingleDetectResult
                    {
                        Name = "线芯数量",
                        Value = int.MinValue.ToString(),// 数值可以填这个，只要保证不在OK的上下限范围即可
                        IsOk = false
                    }
                );
                // 设置“线芯数量”当前值
                SetDetectItemCurValue(ref nodeResult, "线芯数量", int.MinValue.ToString());
            }

            #endregion

            // 定义三个临时列表用于收集实测数值
            List<string> lengths = new List<string>();
            List<string> widths = new List<string>();
            List<string> angles = new List<string>();

            #region 添加“线芯长度”、“线芯宽度”、“线芯角度”和"剥口偏移"检测结果

            if (sortDic.ContainsKey(0))
            {
                List<float> maxYPoints = new List<float>();
                foreach (var item in sortDic[0])
                {
                    // 获取各维度是否OK的状态
                    bool okHeight = GetItemEnableStatus(nodeResult, "线芯长度") ? IsValueOk(item.RotateBox.size.height, heightRange.min, heightRange.max) : true;
                    bool okWidth = GetItemEnableStatus(nodeResult, "线芯宽度") ? IsValueOk(item.RotateBox.size.width, widthRange.min, widthRange.max) : true;
                    bool okAngle = GetItemEnableStatus(nodeResult, "线芯角度") ? IsValueOk(item.RotateBox.angle, angleRange.min, angleRange.max) : true;
                    maxYPoints.Add(GetMaxYOfRotatedRectCorners(item.RotateBox));

                    // 给矩形框打上多个OK/NG判断条件并添加到结果
                    var rotatedRect = new ColorRotatedRect(item.RotateBox);
                    rotatedRect.AddFlag(okHeight);
                    rotatedRect.AddFlag(okWidth);
                    rotatedRect.AddFlag(okAngle);
                        
                    nodeResult.AiResult.CurRectsMap.Add("长宽角度", new List<ColorRotatedRect>());
                    nodeResult.AiResult.CurRectsMap["长宽角度"].Add(rotatedRect);

                    // 收集原始数值用于后续拼接
                    lengths.Add(item.RotateBox.size.height.ToString("F2"));
                    widths.Add(item.RotateBox.size.width.ToString("F2"));
                    angles.Add(item.RotateBox.angle.ToString("F2"));

                    // 添加“线芯长度”检测结果
                    nodeResult.AiResult.DetectResults["线芯长度"].Add(
                        BuildSingleDetectResult(
                            "线芯长度",
                            item.RotateBox.size.height,
                            GetItemEnableStatus(nodeResult, "线芯长度"),
                            heightRange.min,
                            heightRange.max
                        )
                    );

                    // 添加“线芯宽度”检测结果
                    nodeResult.AiResult.DetectResults["线芯宽度"].Add(
                        BuildSingleDetectResult(
                            "线芯宽度",
                            item.RotateBox.size.width,
                            GetItemEnableStatus(nodeResult, "线芯宽度"),
                            widthRange.min,
                            widthRange.max
                        )
                    );

                    // 添加“线芯角度”检测结果
                    nodeResult.AiResult.DetectResults["线芯角度"].Add(
                        BuildSingleDetectResult(
                            "线芯角度",
                            item.RotateBox.angle,
                            GetItemEnableStatus(nodeResult, "线芯角度"),
                            angleRange.min,
                            angleRange.max
                        )
                    );
                }
                // 拼接为字符串
                string lengthStr = string.Join(",", lengths);
                string widthStr = string.Join(",", widths);
                string angleStr = string.Join(",", angles);


                // 添加“剥口偏移”检测结果
                float offset = maxYPoints.Max() - maxYPoints.Min();
                bool islineCoreOffsetOk = IsValueOk(offset, lineCoreOffsetRange.min, lineCoreOffsetRange.max);
                nodeResult.AiResult.DetectResults["剥口偏移"].Add(
                    BuildSingleDetectResult(
                        "剥口偏移",
                        offset,
                        GetItemEnableStatus(nodeResult, "剥口偏移"),
                        lineCoreOffsetRange.min,
                        lineCoreOffsetRange.max
                    )
                );
                float leftBottomX, rightBottomX;
                // 添加要绘制的线
                GetLeftmostAndRightmostBottomXs(nodeResult.AiResult.CurRectsMap["长宽角度"].Select(crr => crr.RotatedRect).ToList(), out leftBottomX, out rightBottomX);
                nodeResult.AiResult.Lines.Add(new ColorLine(new PointF(leftBottomX, maxYPoints.Min()), new PointF(rightBottomX, maxYPoints.Min()),
                    islineCoreOffsetOk ? Color.Green : Color.Red));
                nodeResult.AiResult.Lines.Add(new ColorLine(new PointF(leftBottomX, maxYPoints.Max()), new PointF(rightBottomX, maxYPoints.Max()),
                    islineCoreOffsetOk ? Color.Green : Color.Red));

                // 设置检测项当前值
                SetDetectItemCurValue(ref nodeResult, "线芯长度", lengthStr);
                SetDetectItemCurValue(ref nodeResult, "线芯宽度", widthStr);
                SetDetectItemCurValue(ref nodeResult, "线芯角度", angleStr);
                SetDetectItemCurValue(ref nodeResult, "剥口偏移", offset.ToString("F2"));

            }
            #endregion

            #region 添加绘制的文本

            foreach (var pair in nodeResult.AiResult.DetectResults)
            {
                var isOk = pair.Value.TrueForAll(i => i.IsOk);
                nodeResult.AiResult.Texts.Add(new ColorText(pair.Key + (isOk ? ": OK" : ": NG"), isOk ? Color.Green : Color.Red));
            }

            #endregion
        }

        /// <summary>
        /// 解析胶壳结果
        /// </summary>
        /// <param name="results"></param>
        /// <param name="resultCount"></param>
        /// <param name="nodeResult"></param>
        public static void GelCoatResultParse(List<ObbResult> results, int resultCount, ref NodeResultTDAI nodeResult)
        {
            // 按照 class_id 分组
            var sortDic = SortOBBResults(results, resultCount);

            #region 获取各检测项上下限配置
            var gelCoatCountRange = GetMinMaxValues(nodeResult, "胶壳数量");
            var gelCoatAngleRange = GetMinMaxValues(nodeResult, "胶壳角度");

            var shrapnelCountRange = GetMinMaxValues(nodeResult, "弹片数量");
            var shrapnelLengthRange = GetMinMaxValues(nodeResult, "弹片长度");

            var pinTipCountRange = GetMinMaxValues(nodeResult, "针尖数量");
            var pinTipLengthRange = GetMinMaxValues(nodeResult, "针尖长度");
            var pinTipAngleRange = GetMinMaxValues(nodeResult, "针尖角度");

            var appearanceCountRange = GetMinMaxValues(nodeResult, "外观不良");
            #endregion

            #region 初始化检测项列表
            nodeResult.AiResult.DetectResults["胶壳数量"] = new List<SingleDetectResult>();
            nodeResult.AiResult.DetectResults["胶壳角度"] = new List<SingleDetectResult>();

            nodeResult.AiResult.DetectResults["弹片数量"] = new List<SingleDetectResult>();
            nodeResult.AiResult.DetectResults["弹片长度"] = new List<SingleDetectResult>();

            nodeResult.AiResult.DetectResults["针尖数量"] = new List<SingleDetectResult>();
            nodeResult.AiResult.DetectResults["针尖长度"] = new List<SingleDetectResult>();
            nodeResult.AiResult.DetectResults["针尖角度"] = new List<SingleDetectResult>();

            nodeResult.AiResult.DetectResults["外观不良"] = new List<SingleDetectResult>();
            #endregion

            #region 定义收集有多个实测数值的检测项值
            List<string> shellAngles = new List<string>();
            List<string> shrapnelLens = new List<string>();
            List<string> tipLens = new List<string>();
            List<string> tipAngles = new List<string>();
            #endregion

            #region 检查每个标签是否都有结果，没有的话要构造对应检测项的NG结果

            if (!sortDic.ContainsKey(0)) //"shell"
            {
                nodeResult.AiResult.DetectResults["胶壳数量"].Add(
                    new SingleDetectResult
                    {
                        Name = "胶壳数量",
                        Value = int.MinValue.ToString(),
                        IsOk = false
                    }
                );
                // 设置“胶壳数量”当前值
                SetDetectItemCurValue(ref nodeResult, "胶壳数量", int.MinValue.ToString());
            }

            if (!sortDic.ContainsKey(1)) //"shrapnel"
            {
                nodeResult.AiResult.DetectResults["弹片数量"].Add(
                    new SingleDetectResult
                    {
                        Name = "弹片数量",
                        Value = int.MinValue.ToString(),
                        IsOk = false
                    }
                );
                // 设置“弹片数量”当前值
                SetDetectItemCurValue(ref nodeResult, "弹片数量", int.MinValue.ToString());
            }

            if (!sortDic.ContainsKey(2)) //"tip"
            {
                nodeResult.AiResult.DetectResults["针尖数量"].Add(
                    new SingleDetectResult
                    {
                        Name = "针尖数量",
                        Value = int.MinValue.ToString(),
                        IsOk = false
                    }
                );
                // 设置“针尖数量”当前值
                SetDetectItemCurValue(ref nodeResult, "针尖数量", int.MinValue.ToString());
            }
            // ng标签不用
            if (!sortDic.ContainsKey(3)) //"ng"
            {
                nodeResult.AiResult.DetectResults["外观不良"].Add(
                    new SingleDetectResult
                    {
                        Name = "外观不良",
                        Value = int.MinValue.ToString(),
                        IsOk = false
                    }
                );
                // 设置“外观不良”当前值
                SetDetectItemCurValue(ref nodeResult, "外观不良", int.MinValue.ToString());
            }

            #endregion

            #region 添加“胶壳数量”检测结果

            if (sortDic.ContainsKey(0))
            {
                var res0 = sortDic[0].Count;
                nodeResult.AiResult.DetectResults["胶壳数量"].Add(
                    BuildSingleDetectResult(
                        "胶壳数量",
                        res0,
                        GetItemEnableStatus(nodeResult, "胶壳数量"),
                        gelCoatCountRange.min,
                        gelCoatCountRange.max
                    )
                );
                // 设置“胶壳数量”当前值
                SetDetectItemCurValue(ref nodeResult, "胶壳数量", res0.ToString());
            }

            #endregion

            #region 添加“胶壳角度”检测结果（遍历）

            if (sortDic.ContainsKey(0))
            {
                string itemName = "胶壳角度";
                foreach (var item in sortDic[0])
                {
                    float angle = item.RotateBox.angle;
                    bool okAngle = GetItemEnableStatus(nodeResult, itemName) ? IsValueOk(angle, gelCoatAngleRange.min, gelCoatAngleRange.max) : true;

                    // 给矩形框打上OK/NG判断条件并添加到结果
                    var rotatedRect = new ColorRotatedRect(item.RotateBox);
                    rotatedRect.AddFlag(okAngle);
                    nodeResult.AiResult.CurRectsMap.Add(itemName, new List<ColorRotatedRect>());
                    nodeResult.AiResult.CurRectsMap[itemName].Add(rotatedRect);

                    shellAngles.Add(item.RotateBox.angle.ToString("F2"));

                    nodeResult.AiResult.DetectResults[itemName].Add(
                        BuildSingleDetectResult(
                            itemName,
                            angle,
                            GetItemEnableStatus(nodeResult, itemName),
                            gelCoatAngleRange.min,
                            gelCoatAngleRange.max
                        )
                    );
                }
            }
            #endregion

            #region 添加“弹片数量”检测结果

            if (sortDic.ContainsKey(1))
            {
                var res1 = sortDic[1].Count;
                nodeResult.AiResult.DetectResults["弹片数量"].Add(
                    BuildSingleDetectResult(
                        "弹片数量",
                        res1,
                        GetItemEnableStatus(nodeResult, "弹片数量"),
                        shrapnelCountRange.min,
                        shrapnelCountRange.max
                    )
                );
                // 设置“弹片数量”当前值
                SetDetectItemCurValue(ref nodeResult, "弹片数量", res1.ToString());
            }
            #endregion

            #region 添加“弹片长度”检测结果（遍历）
            if (sortDic.ContainsKey(1))
            {
                string itemName = "弹片长度";
                foreach (var item in sortDic[1])
                {
                    float length = item.RotateBox.size.height;
                    bool okLength = GetItemEnableStatus(nodeResult, "弹片长度") ? IsValueOk(length, shrapnelLengthRange.min, shrapnelLengthRange.max) : true;

                    // 给矩形框打上OK/NG判断条件并添加到结果
                    var rotatedRect = new ColorRotatedRect(item.RotateBox);
                    rotatedRect.AddFlag(okLength);
                    nodeResult.AiResult.CurRectsMap.Add(itemName, new List<ColorRotatedRect>());
                    nodeResult.AiResult.CurRectsMap[itemName].Add(rotatedRect);

                    shrapnelLens.Add(length.ToString("F2"));

                    nodeResult.AiResult.DetectResults["弹片长度"].Add(
                        BuildSingleDetectResult(
                            "弹片长度",
                            length,
                            GetItemEnableStatus(nodeResult, "弹片长度"),
                            shrapnelLengthRange.min,
                            shrapnelLengthRange.max
                        )
                    );
                }
            }
            #endregion

            #region 添加“针尖数量”检测结果

            if (sortDic.ContainsKey(2))
            {
                var res2 = sortDic.ContainsKey(2) ? sortDic[2].Count : 0;
                nodeResult.AiResult.DetectResults["针尖数量"].Add(
                    BuildSingleDetectResult(
                        "针尖数量",
                        res2,
                        GetItemEnableStatus(nodeResult, "针尖数量"),
                        pinTipCountRange.min,
                        pinTipCountRange.max
                    )
                );
                // 设置“针尖数量”当前值
                SetDetectItemCurValue(ref nodeResult, "针尖数量", res2.ToString());
            }
            #endregion

            #region 添加“针尖长度”和“针尖角度”检测结果
            if (sortDic.ContainsKey(2))
            {
                string itemName1 = "针尖长度";
                string itemName2 = "针尖角度";
                foreach (var item in sortDic[2])
                {
                    float length = item.RotateBox.size.height;
                    bool okLength = GetItemEnableStatus(nodeResult, itemName1) ? IsValueOk(length, pinTipLengthRange.min, pinTipLengthRange.max) : true;

                    float angle = item.RotateBox.angle;
                    bool okAngle = GetItemEnableStatus(nodeResult, itemName2) ? IsValueOk(angle, pinTipAngleRange.min, pinTipAngleRange.max) : true;

                    // 给矩形框打上OK/NG判断条件并添加到结果
                    var rotatedRect = new ColorRotatedRect(item.RotateBox);
                    rotatedRect.AddFlag(okLength);
                    rotatedRect.AddFlag(okAngle);
                    nodeResult.AiResult.CurRectsMap.Add(itemName1, new List<ColorRotatedRect>());
                    nodeResult.AiResult.CurRectsMap[itemName1].Add(rotatedRect);

                    tipLens.Add(length.ToString("F2"));
                    tipAngles.Add(angle.ToString("F2"));

                    nodeResult.AiResult.DetectResults[itemName1].Add(
                        BuildSingleDetectResult(
                            itemName1,
                            length,
                            GetItemEnableStatus(nodeResult, itemName1),
                            pinTipLengthRange.min,
                            pinTipLengthRange.max
                        )
                    );

                    nodeResult.AiResult.DetectResults[itemName2].Add(
                        BuildSingleDetectResult(
                            itemName2,
                            angle,
                            GetItemEnableStatus(nodeResult, itemName2),
                            pinTipAngleRange.min,
                            pinTipAngleRange.max
                        )
                    );
                }
            }
            #endregion

            #region 添加“外观不良”检测结果

            if (sortDic.ContainsKey(3))
            {
                var res3 = sortDic[3].Count;
                nodeResult.AiResult.DetectResults["外观不良"].Add(
                    BuildSingleDetectResult(
                        "外观不良",
                        res3,
                        GetItemEnableStatus(nodeResult, "外观不良"),
                        appearanceCountRange.min,
                        appearanceCountRange.max
                    )
                );
                // 设置“外观不良”当前值
                SetDetectItemCurValue(ref nodeResult, "外观不良", res3.ToString());
            }

            #endregion

            // 设置检测项当前值
            if (sortDic.ContainsKey(0) && sortDic.ContainsKey(1) && sortDic.ContainsKey(2))
            {
                string shellAnglesStr = string.Join(",", shellAngles);
                string shrapnelLensStr = string.Join(",", shrapnelLens);
                string tipLensStr = string.Join(",", tipLens);
                string tipAnglesStr = string.Join(",", tipAngles);
                SetDetectItemCurValue(ref nodeResult, "胶壳角度", shellAnglesStr);
                SetDetectItemCurValue(ref nodeResult, "弹片长度", shrapnelLensStr);
                SetDetectItemCurValue(ref nodeResult, "针尖长度", tipLensStr);
                SetDetectItemCurValue(ref nodeResult, "针尖角度", tipAnglesStr);
            }

            #region 添加绘制的文本

            foreach (var pair in nodeResult.AiResult.DetectResults)
            {
                var isOk = pair.Value.TrueForAll(i => i.IsOk);
                nodeResult.AiResult.Texts.Add(new ColorText(pair.Key + (isOk ? ": OK" : ": NG"), isOk ? Color.Green : Color.Red));
            }

            #endregion
        }

        /// <summary>
        /// 获取旋转矩形列表中最左边矩形的左下顶点的x，和最右边矩形的右下顶点的x
        /// </summary>
        /// <param name="rects"></param>
        /// <param name="leftBottomX"></param>
        /// <param name="rightBottomX"></param>
        /// <returns></returns>
        public static bool GetLeftmostAndRightmostBottomXs(List<RotatedRect> rects, out float leftBottomX, out float rightBottomX)
        {
            leftBottomX = 0;
            rightBottomX = 0;

            if (rects == null || rects.Count == 0)
                return false;

            // 找出最左边和最右边的矩形
            var leftmostRect = rects.OrderBy(r => r.center.x).First();
            var rightmostRect = rects.OrderByDescending(r => r.center.x).First();

            // 获取它们的四个角点
            PointF[] leftCorners = GetRotatedRectCorners(leftmostRect);
            PointF[] rightCorners = GetRotatedRectCorners(rightmostRect);

            // 找出底边两个点（y 最大的两个点）
            var leftBottomPoints = leftCorners.OrderByDescending(p => p.Y).Take(2).ToArray();
            var rightBottomPoints = rightCorners.OrderByDescending(p => p.Y).Take(2).ToArray();

            // 左边矩形：按 x 排序，取最小的是左下点
            Array.Sort(leftBottomPoints, (a, b) => a.X.CompareTo(b.X));
            leftBottomX = leftBottomPoints[0].X;

            // 右边矩形：按 x 排序，取最大的是右下点
            Array.Sort(rightBottomPoints, (a, b) => b.X.CompareTo(a.X));
            rightBottomX = rightBottomPoints[0].X;

            return true;
        }

        /// <summary>
        /// 设置检测项当前实测值
        /// </summary>
        private static void SetDetectItemCurValue(ref NodeResultTDAI nodeResultTDAI, string detectItemName, string value)
        {
            var detectItem = nodeResultTDAI.AIInputInfo.DetectItems.FirstOrDefault(x => x.Name == detectItemName);
            if (detectItem != null)
            {
                detectItem.CurValue = value;
            }
        }
        /// <summary>
        /// 获取检测项是否启用的状态
        /// </summary>
        /// <param name="nodeResult"></param>
        /// <param name="itemName"></param>
        /// <returns></returns>
        private static bool GetItemEnableStatus(NodeResultTDAI nodeResult, string itemName)
        {
            return nodeResult.AIInputInfo.DetectItems.FirstOrDefault(x => x.Name == itemName)?.Enable ?? false;
        }
        /// <summary>
        /// 获取检测项信息并解析 MinValue 和 MaxValue
        /// </summary>
        /// <param name="nodeResult"></param>
        /// <param name="itemName"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private static (double min, double max) GetMinMaxValues(NodeResultTDAI nodeResult, string itemName)
        {
            var itemInfo = nodeResult.AIInputInfo.DetectItems.FirstOrDefault(x => x.Name == itemName);
            if (itemInfo == null)
                throw new Exception($"配置中找不到名为“{itemName}”对应的检测项！");

            return (double.Parse(itemInfo.MinValue), double.Parse(itemInfo.MaxValue));
        }
        /// <summary>
        /// 构建 SingleDetectResult 对象
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="isOk"></param>
        /// <param name="enable"></param>
        /// <param name="minV"></param>
        /// <param name="maxV"></param>
        /// <returns></returns>
        private static SingleDetectResult BuildSingleDetectResult(string name, float value, bool enable, double minV, double maxV)
        {
            return new SingleDetectResult
            {
                Name = name,
                Value = value.ToString("F2"),
                IsOk = enable ? IsValueOk(value, minV, maxV) : true
            };
        }
        private static SingleDetectResult BuildSingleDetectResult(string name, int value, bool enable, double minV, double maxV)
        {
            return new SingleDetectResult
            {
                Name = name,
                Value = value.ToString(),
                IsOk = enable ? IsValueOk(value, minV, maxV) : true
            };
        }
        /// <summary>
        /// 获取旋转矩形的四个顶点中的最大Y值
        /// </summary>
        /// <param name="rect"></param>
        /// <returns></returns>
        public static float GetMaxYOfRotatedRectCorners(RotatedRect rect)
        {
            // 获取四个角点
            PointF[] corners = GetRotatedRectCorners(rect);

            // 取 y 坐标最大值
            return corners.Max(p => p.Y);
        }
        /// <summary>
        /// 获取旋转矩形的四个顶点
        /// </summary>
        /// <param name="rect"></param>
        /// <returns></returns>
        public static PointF[] GetRotatedRectCorners(RotatedRect rect)
        {
            PointF[] corners = new PointF[4];

            float angleRad = rect.angle * (float)Math.PI / 180.0f;
            float cosA = (float)Math.Cos(angleRad);
            float sinA = (float)Math.Sin(angleRad);

            float cx = rect.center.x;
            float cy = rect.center.y;

            float w2 = rect.size.width / 2.0f;
            float h2 = rect.size.height / 2.0f;

            // 四个未旋转的顶点（相对于中心）
            PointF[] unrotated = new PointF[]
            {
        new PointF(-w2, -h2),
        new PointF( w2, -h2),
        new PointF( w2,  h2),
        new PointF(-w2,  h2)
            };

            for (int i = 0; i < 4; i++)
            {
                float x = unrotated[i].X;
                float y = unrotated[i].Y;

                // 应用旋转
                float rx = x * cosA - y * sinA + cx;
                float ry = x * sinA + y * cosA + cy;

                corners[i] = new PointF(rx, ry);
            }

            return corners;
        }
        /// <summary>
        /// 排序OBB结果
        /// </summary>
        private static SortedDictionary<int, List<ObbResult>> SortOBBResults(List<ObbResult> results, int resultCount)
        {
            Dictionary<int, List<ObbResult>> ResMap = new Dictionary<int, List<ObbResult>>();
            // 遍历每一个检出结果，根据class_id将结果保存到对应列表中
            for (int i = 0; i < resultCount; ++i)
            {
                ObbResult result = results[i];
                // 判断该id是否存在,不存在需要先创建列表才能添加，防止nul异常
                if (!ResMap.ContainsKey(result.ClassId))
                {
                    ResMap[result.ClassId] = new List<ObbResult>();
                }
                ResMap[result.ClassId].Add(result);
            }
            // 使用LINQ按键class_id从小到大进行排序
            return new SortedDictionary<int, List<ObbResult>>(ResMap);
        }
        /// <summary>
        /// 处理OBB模型输出数据
        /// 输出的 OBB 结果旋转矩形可能会交换宽高值，以及角度需要调整为实际的角度
        /// </summary>
        public static void ProcessingRotatedRects(ref List<ObbResult> obbResults)
        {
            for (int i = 0; i < obbResults.Count; i++)
            {
                // 获取当前项
                ObbResult result = obbResults[i];
                var box = result.RotateBox;
                float angleDeg = box.angle;

                // 如果角度绝对值超过 135°，需要旋转 180° 不需要交换宽高
                if (Math.Abs(angleDeg) > 135)
                {
                    // 180°-检出角度
                    box.angle = 180.0f - angleDeg;
                }// 如果角度绝对值超过 45°，需要旋转 90° 并交换宽高
                else if (Math.Abs(angleDeg) > 45)
                {
                    // 90°-检出角度
                    box.angle = 90.0f - angleDeg;

                    // 交换 width 和 height
                    float temp = box.size.width;
                    box.size.width = box.size.height;
                    box.size.height = temp;
                }

                // 更新 RotateBox 后写回原列表
                result.RotateBox = box;
                obbResults[i] = result;
            }
        }
        /// <summary>
        /// 判断当前值是否OK
        /// </summary>
        /// <param name="curV">当前值</param>
        /// <param name="minV">最小值</param>
        /// <param name="maxV">最大值</param>
        /// <returns></returns>
        private static bool IsValueOk(double curV, double minV, double maxV)
        {
            if (curV >= minV && curV <= maxV)
                return true;
            return false;
        }
    }
}
