using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Web.UI;
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
        /// 解析线芯检测结果
        /// </summary>
        public static void LineCoreResultParse(List<ObbResult> results, int resultCount, ref NodeResultTDAI nodeResult)
        {
            // 按照class_id分组
            var sortDic = SortOBBResults(results, resultCount);

            // 获取检测项的上下限
            var lineCoreRange = GetMinMaxValues(nodeResult, "线芯数量");
            var heightRange = GetMinMaxValues(nodeResult, "线芯长度");
            var widthRange = GetMinMaxValues(nodeResult, "线芯宽度");
            var angleRange = GetMinMaxValues(nodeResult, "线芯角度");

            // 初始化 DetectResults 列表
            nodeResult.AiResult.DetectResults["线芯数量"] = new List<SingleDetectResult>();
            nodeResult.AiResult.DetectResults["线芯长度"] = new List<SingleDetectResult>();
            nodeResult.AiResult.DetectResults["线芯宽度"] = new List<SingleDetectResult>();
            nodeResult.AiResult.DetectResults["线芯角度"] = new List<SingleDetectResult>();


            // 添加“线芯数量”检测结果
            bool okLineCore = IsValueOk(sortDic[0].Count, lineCoreRange.min, lineCoreRange.max);
            nodeResult.AiResult.DetectResults["线芯数量"].Add(
                BuildSingleDetectResult(
                    "线芯数量",
                    sortDic[0].Count,
                    okLineCore,
                    nodeResult.AIInputInfo.DetectItems.FirstOrDefault(x => x.Name == "线芯数量").Enable,
                    lineCoreRange.min,
                    lineCoreRange.max
                )
            );
            // 设置“线芯数量”当前值
            SetDetectItemCurValue(ref nodeResult, "线芯数量", sortDic[0].Count.ToString("F2"));

            // 定义三个临时列表用于收集实测数值
            List<string> lengths = new List<string>();
            List<string> widths = new List<string>();
            List<string> angles = new List<string>();

            // 遍历线芯标签结果
            foreach (var item in sortDic[0])
            {
                // 获取各维度是否OK的状态
                bool okHeight = IsValueOk(item.RotateBox.size.height, heightRange.min, heightRange.max);
                bool okWidth = IsValueOk(item.RotateBox.size.width, widthRange.min, widthRange.max);
                bool okAngle = IsValueOk(item.RotateBox.angle, angleRange.min, angleRange.max);

                // 给矩形框打上多个OK/NG判断条件并添加到结果
                var rotatedRect = new ColorRotatedRect(item.RotateBox);
                rotatedRect.AddFlag(okHeight);
                rotatedRect.AddFlag(okWidth);
                nodeResult.AiResult.Rects.Add(rotatedRect);

                // 收集原始数值用于后续拼接
                lengths.Add(item.RotateBox.size.height.ToString("F2"));
                widths.Add(item.RotateBox.size.width.ToString("F2"));
                angles.Add(item.RotateBox.angle.ToString("F2"));

                // 添加“线芯长度”检测结果
                nodeResult.AiResult.DetectResults["线芯长度"].Add(
                    BuildSingleDetectResult(
                        "线芯长度",
                        item.RotateBox.size.height,
                        okHeight,
                        nodeResult.AIInputInfo.DetectItems.FirstOrDefault(x => x.Name == "线芯长度").Enable,
                        heightRange.min,
                        heightRange.max
                    )
                );

                // 添加“线芯宽度”检测结果
                nodeResult.AiResult.DetectResults["线芯宽度"].Add(
                    BuildSingleDetectResult(
                        "线芯宽度",
                        item.RotateBox.size.width,
                        okWidth,
                        nodeResult.AIInputInfo.DetectItems.FirstOrDefault(x => x.Name == "线芯宽度").Enable,
                        widthRange.min,
                        widthRange.max
                    )
                );

                // 添加“线芯角度”检测结果
                nodeResult.AiResult.DetectResults["线芯角度"].Add(
                    BuildSingleDetectResult(
                        "线芯角度",
                        item.RotateBox.angle,
                        okAngle,
                        nodeResult.AIInputInfo.DetectItems.FirstOrDefault(x => x.Name == "线芯角度").Enable,
                        angleRange.min,
                        angleRange.max
                    )
                );
            }
            // 拼接为字符串
            string lengthStr = string.Join(",", lengths);
            string widthStr = string.Join(",", widths);
            string angleStr = string.Join(",", angles);

            // 设置检测项当前值
            SetDetectItemCurValue(ref nodeResult, "线芯数量", lengthStr);
            SetDetectItemCurValue(ref nodeResult, "线芯宽度", widthStr);
            SetDetectItemCurValue(ref nodeResult, "线芯角度", angleStr);

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

            #region 添加“胶壳数量”检测结果
            var res0 = sortDic.ContainsKey(0) ? sortDic[0].Count : 0;
            bool okGelCoatCount = IsValueOk(res0, gelCoatCountRange.min, gelCoatCountRange.max);
            nodeResult.AiResult.DetectResults["胶壳数量"].Add(
                BuildSingleDetectResult(
                    "胶壳数量",
                    res0,
                    okGelCoatCount,
                    GetItemEnableStatus(nodeResult, "胶壳数量"),
                    gelCoatCountRange.min,
                    gelCoatCountRange.max
                )
            );
            // 设置“胶壳数量”当前值
            SetDetectItemCurValue(ref nodeResult, "胶壳数量", res0.ToString("F2"));
            #endregion

            #region 添加“胶壳角度”检测结果（遍历）
            if (sortDic.ContainsKey(0))
            {
                foreach (var item in sortDic[0])
                {
                    float angle = item.RotateBox.angle;
                    bool okAngle = IsValueOk(angle, gelCoatAngleRange.min, gelCoatAngleRange.max);

                    // 给矩形框打上OK/NG判断条件并添加到结果
                    var rotatedRect = new ColorRotatedRect(item.RotateBox);
                    rotatedRect.AddFlag(okAngle);
                    nodeResult.AiResult.Rects.Add(rotatedRect);

                    shellAngles.Add(item.RotateBox.angle.ToString("F2"));

                    nodeResult.AiResult.DetectResults["胶壳角度"].Add(
                        BuildSingleDetectResult(
                            "胶壳角度",
                            angle,
                            okAngle,
                            GetItemEnableStatus(nodeResult, "胶壳角度"),
                            gelCoatAngleRange.min,
                            gelCoatAngleRange.max
                        )
                    );
                }
            }
            #endregion

            #region 添加“弹片数量”检测结果
            var res1 = sortDic.ContainsKey(0) ? sortDic[1].Count : 0;
            bool okShrapnelCount = IsValueOk(res1, shrapnelCountRange.min, shrapnelCountRange.max);
            nodeResult.AiResult.DetectResults["弹片数量"].Add(
                BuildSingleDetectResult(
                    "弹片数量",
                    res1,
                    okShrapnelCount,
                    GetItemEnableStatus(nodeResult, "弹片数量"),
                    shrapnelCountRange.min,
                    shrapnelCountRange.max
                )
            );
            // 设置“弹片数量”当前值
            SetDetectItemCurValue(ref nodeResult, "弹片数量", res1.ToString("F2"));
            #endregion

            #region 添加“弹片长度”检测结果（遍历）
            if (sortDic.ContainsKey(1))
            {
                foreach (var item in sortDic[1])
                {
                    float length = item.RotateBox.size.height;
                    bool okLength = IsValueOk(length, shrapnelLengthRange.min, shrapnelLengthRange.max);

                    // 给矩形框打上OK/NG判断条件并添加到结果
                    var rotatedRect = new ColorRotatedRect(item.RotateBox);
                    rotatedRect.AddFlag(okLength);
                    nodeResult.AiResult.Rects.Add(rotatedRect);

                    shrapnelLens.Add(length.ToString("F2"));

                    nodeResult.AiResult.DetectResults["弹片长度"].Add(
                        BuildSingleDetectResult(
                            "弹片长度",
                            length,
                            okLength,
                            GetItemEnableStatus(nodeResult, "弹片长度"),
                            shrapnelLengthRange.min,
                            shrapnelLengthRange.max
                        )
                    );
                }
            }
            #endregion

            #region 添加“针尖数量”检测结果
            var res2 = sortDic.ContainsKey(0) ? sortDic[2].Count : 0;
            bool okPinTipCount = IsValueOk(res2, pinTipCountRange.min, pinTipCountRange.max);
            nodeResult.AiResult.DetectResults["针尖数量"].Add(
                BuildSingleDetectResult(
                    "针尖数量",
                    res2,
                    okPinTipCount,
                    GetItemEnableStatus(nodeResult, "针尖数量"),
                    pinTipCountRange.min,
                    pinTipCountRange.max
                )
            );
            // 设置“针尖数量”当前值
            SetDetectItemCurValue(ref nodeResult, "针尖数量", res2.ToString("F2"));
            #endregion

            #region 添加“针尖长度”检测结果（遍历）
            if (sortDic.ContainsKey(2))
            {
                foreach (var item in sortDic[2])
                {
                    float length = item.RotateBox.size.height;
                    bool okLength = IsValueOk(length, pinTipLengthRange.min, pinTipLengthRange.max);

                    // 给矩形框打上OK/NG判断条件并添加到结果
                    var rotatedRect = new ColorRotatedRect(item.RotateBox);
                    rotatedRect.AddFlag(okLength);
                    nodeResult.AiResult.Rects.Add(rotatedRect);

                    tipLens.Add(length.ToString("F2"));

                    nodeResult.AiResult.DetectResults["针尖长度"].Add(
                        BuildSingleDetectResult(
                            "针尖长度",
                            length,
                            okLength,
                            GetItemEnableStatus(nodeResult, "针尖长度"),
                            pinTipLengthRange.min,
                            pinTipLengthRange.max
                        )
                    );
                }
            }
            #endregion

            #region 添加“针尖角度”检测结果（遍历）
            if (sortDic.ContainsKey(2))
            {
                foreach (var item in sortDic[2])
                {
                    float angle = item.RotateBox.angle;
                    bool okAngle = IsValueOk(angle, pinTipAngleRange.min, pinTipAngleRange.max);

                    // 给矩形框打上OK/NG判断条件并添加到结果
                    var rotatedRect = new ColorRotatedRect(item.RotateBox);
                    rotatedRect.AddFlag(okAngle);
                    nodeResult.AiResult.Rects.Add(rotatedRect);

                    tipAngles.Add(angle.ToString("F2"));

                    nodeResult.AiResult.DetectResults["针尖角度"].Add(
                        BuildSingleDetectResult(
                            "针尖角度",
                            angle,
                            okAngle,
                            GetItemEnableStatus(nodeResult, "针尖角度"),
                            pinTipAngleRange.min,
                            pinTipAngleRange.max
                        )
                    );
                }
            }
            #endregion

            #region 添加“外观不良”检测结果
            var res3 = sortDic.ContainsKey(0) ? sortDic[0].Count : 0;
            bool okAppearance = IsValueOk(sortDic.ContainsKey(3) ? sortDic[3].Count : 0, appearanceCountRange.min, appearanceCountRange.max);
            nodeResult.AiResult.DetectResults["外观不良"].Add(
                BuildSingleDetectResult(
                    "外观不良",
                    sortDic.ContainsKey(3) ? sortDic[3].Count : 0,
                    okAppearance,
                    GetItemEnableStatus(nodeResult, "外观不良"),
                    appearanceCountRange.min,
                    appearanceCountRange.max
                )
            );
            // 设置“外观不良”当前值
            SetDetectItemCurValue(ref nodeResult, "外观不良", res3.ToString("F2"));
            #endregion

            #region 添加结果矩形框

            // 给矩形框打上多个OK/NG判断条件并添加到结果
            //var rotatedRect = new ColorRotatedRect(item.RotateBox);
            //rotatedRect.AddFlag(okHeight);
            //rotatedRect.AddFlag(okWidth);
            //nodeResult.AiResult.Rects.Add(rotatedRect);

            #endregion

            // 设置检测项当前值
            string shellAnglesStr = string.Join(",", shellAngles);
            string shrapnelLensStr = string.Join(",", shrapnelLens);
            string tipLensStr = string.Join(",", tipLens);
            string tipAnglesStr = string.Join(",", tipAngles);
            SetDetectItemCurValue(ref nodeResult, "胶壳角度", shellAnglesStr);
            SetDetectItemCurValue(ref nodeResult, "弹片长度", shrapnelLensStr);
            SetDetectItemCurValue(ref nodeResult, "针尖长度", tipLensStr);
            SetDetectItemCurValue(ref nodeResult, "针尖角度", tipAnglesStr);
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
        private static(double min, double max) GetMinMaxValues(NodeResultTDAI nodeResult, string itemName)
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
        private static SingleDetectResult BuildSingleDetectResult(string name, float value, bool isOk, bool enable, double minV, double maxV)
        {
            return new SingleDetectResult
            {
                Name = name,
                Value = value.ToString("F2"),
                IsOk = enable ? IsValueOk(value, minV, maxV) : true
            };
        }
        /// <summary>
        /// 添加检出数量一类的结果
        /// </summary>
        /// <param name="nodeResult"></param>
        /// <param name="itemName"></param>
        /// <param name="classId"></param>
        /// <param name="sortDic"></param>
        /// <exception cref="Exception"></exception>
        private static void AddDetectionResultOfNum(NodeResultTDAI nodeResult, string itemName, int classId, SortedDictionary<int, List<ObbResult>> sortDic)
        {
            try
            {

                // 获取检测项配置
                var itemInfo = nodeResult.AIInputInfo.DetectItems.FirstOrDefault(x => x.Name == itemName);
                if (itemInfo == null)
                    throw new Exception($"配置中找不到名为“{itemName}”对应的检测项！");

                // 解析最小最大值
                double minV = double.Parse(itemInfo.MinValue);
                double maxV = double.Parse(itemInfo.MaxValue);

                // 获取当前类别数量
                int count = sortDic.ContainsKey(classId) ? sortDic[classId].Count : 0;

                // 构建检测结果
                var detectResult = new SingleDetectResult
                {
                    Name = itemName,
                    Value = count.ToString(),
                    IsOk = itemInfo.Enable ? IsValueOk(count, minV, maxV) : true
                };

                // 写入结果字典（替换原有列表）
                if(itemInfo.Enable)
                    nodeResult.AiResult.DetectResults[itemName] = new List<SingleDetectResult> { detectResult };
            }
            catch (Exception)
            {
                throw;
            }
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

                // 如果角度绝对值超过 45°，说明矩形竖直方向为主，需要旋转 90° 并交换宽高
                if (Math.Abs(angleDeg) > 45)
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
