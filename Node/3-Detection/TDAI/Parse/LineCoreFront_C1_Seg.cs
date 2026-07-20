using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Logger;
using OpenCvSharp;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

namespace TDJS_Vision.Node._3_Detection.TDAI.Parse
{
    /// <summary>
    /// 线芯正面截面SEG模型结果解析器。
    /// </summary>
    public class LineCoreFront_C1_Seg
    {
        /// <summary>
        /// 自动学习已累计的样本数量。
        /// </summary>
        private static int _count = 0;

        /// <summary>
        /// 自动学习模式下按检测项缓存的实测数据。
        /// </summary>
        private static Dictionary<string, List<float>> _autoStudyDatas = new Dictionary<string, List<float>>();

        /// <summary>
        /// 解析线芯截面模型结果。
        /// </summary>
        /// <param name="results">SEG模型检出结果。</param>
        /// <param name="resultCount">模型结果数量，保留用于兼容旧调用。</param>
        /// <param name="param">AI节点参数。</param>
        /// <param name="process">当前流程对象，用于累计OK/NG数量。</param>
        /// <param name="nodeResult">AI节点运行结果。</param>
        public static void Parse(List<SegResult> results, int resultCount, NodeParamTDAI param, Process process, ref NodeResultTDAI nodeResult)
        {
            results = results ?? new List<SegResult>();

            // 当前使用的检测项配置。
            var deteItems = Solution.Instance.DetectItemDic[param.CurDetectItemName];
            ParseCommon.NormalizeDetectItems(deteItems);

            // 保存检测结果、颜色文本和已添加框，避免同一个目标重复绘制。
            var detectResults = new Dictionary<string, List<SingleDetectResult>>();
            var texts = new List<ColorText>();
            HashSet<string> addedRectKeys = new HashSet<string>();

            foreach (var item in deteItems)
            {
                if (!item.Enable)
                    continue;

                string itemName = item.Name;
                bool hasData = false;

                Func<SegResult, string> extractValueFunc = CreateExtractValueFunc(itemName);
                if (extractValueFunc == null)
                    continue;

                var matchedResults = results.Where(r => extractValueFunc(r) != null).ToList();
                var singleResults = new List<SingleDetectResult>();

                if (item.IsCountItem)
                {
                    hasData = ParseCountItem(matchedResults, item, itemName, param, deteItems, addedRectKeys, texts, singleResults, ref nodeResult);
                }
                else
                {
                    hasData = ParseGatheringItem(matchedResults, item, itemName, param, deteItems, addedRectKeys, texts, singleResults, ref nodeResult);
                }

                if (hasData)
                    detectResults[itemName] = singleResults;
            }

            nodeResult.AlgorithmResult.DetectResults = detectResults;

            bool allOk = detectResults.Values
                .SelectMany(list => list)
                .All(r => r.IsOk);

            if (allOk)
                process.OKNumber++;
            else
                process.NGNumber++;

            texts.Add(new ColorText(
                LanguageManager.Format("Result.TotalCount", process.OKNumber + process.NGNumber),
                Color.Green));

            texts.Add(new ColorText(
                LanguageManager.Format("Result.OKCount", process.OKNumber),
                Color.Green));

            texts.Add(new ColorText(
                LanguageManager.Format("Result.Yield", ((process.OKNumber * 1.0) / (process.OKNumber + process.NGNumber) * 100).ToString("F2")),
                Color.Green));

            texts.Add(new ColorText(
                LanguageManager.Format("Result.ProductResult", allOk ? "OK" : "NG"),
                allOk ? Color.Green : Color.Red));

            nodeResult.AlgorithmResult.Texts = texts;

            FinishAutoStudyIfNeeded(deteItems, param);
        }

        /// <summary>
        /// 根据检测项名称创建SEG结果取值函数。
        /// </summary>
        /// <param name="itemName">检测项名称或语言键。</param>
        /// <returns>匹配结果取值函数；无法匹配时返回空。</returns>
        private static Func<SegResult, string> CreateExtractValueFunc(string itemName)
        {
            switch (itemName)
            {
                case "DetectItem.CoreCount":
                    return r => r.ClassId == 0 ? "1" : null;

                case "DetectItem.CoreGathering":
                    return r => r.ClassId == 0 ? $"{r.Rect.X}-{r.Rect.Y}-{r.Rect.Width}-{r.Rect.Height}" : null;

                default:
                    return null;
            }
        }

        /// <summary>
        /// 解析数量型检测项。
        /// </summary>
        /// <param name="matchedResults">当前检测项匹配到的模型结果。</param>
        /// <param name="item">检测项配置。</param>
        /// <param name="itemName">检测项名称。</param>
        /// <param name="param">AI节点参数。</param>
        /// <param name="deteItems">当前检测模板内所有检测项。</param>
        /// <param name="addedRectKeys">已绘制矩形去重集合。</param>
        /// <param name="texts">颜色文本集合。</param>
        /// <param name="singleResults">单检测项结果集合。</param>
        /// <param name="nodeResult">AI节点运行结果。</param>
        /// <returns>是否产生了检测结果。</returns>
        private static bool ParseCountItem(
            List<SegResult> matchedResults,
            DetectItemInfo item,
            string itemName,
            NodeParamTDAI param,
            List<DetectItemInfo> deteItems,
            HashSet<string> addedRectKeys,
            List<ColorText> texts,
            List<SingleDetectResult> singleResults,
            ref NodeResultTDAI nodeResult)
        {
            int count = matchedResults.Count;
            string valueStr = count.ToString();

            AddAutoStudyValue(itemName, count, param);

            bool minOk = float.TryParse(item.MinValue, out float min) && count >= min;
            bool maxOk = float.TryParse(item.MaxValue, out float max) && count <= max;
            bool isOk = param.IsAutoStudy || (count > 0 && minOk && maxOk);

            singleResults.Add(new SingleDetectResult
            {
                Name = itemName,
                Value = valueStr,
                IsOk = isOk
            });

            foreach (var aiRes in matchedResults)
                ParseCommon.AddRect(addedRectKeys, aiRes.Rect, isOk, ref nodeResult);

            AddDetectText(texts, deteItems, item, itemName, valueStr, isOk);
            ParseCommon.SetDetectItemCurValue(ref param, itemName, valueStr);

            return true;
        }

        /// <summary>
        /// 解析线芯聚拢度检测项。
        /// </summary>
        /// <param name="matchedResults">当前检测项匹配到的模型结果。</param>
        /// <param name="item">检测项配置。</param>
        /// <param name="itemName">检测项名称。</param>
        /// <param name="param">AI节点参数。</param>
        /// <param name="deteItems">当前检测模板内所有检测项。</param>
        /// <param name="addedRectKeys">已绘制矩形去重集合。</param>
        /// <param name="texts">颜色文本集合。</param>
        /// <param name="singleResults">单检测项结果集合。</param>
        /// <param name="nodeResult">AI节点运行结果。</param>
        /// <returns>是否产生了检测结果。</returns>
        private static bool ParseGatheringItem(
            List<SegResult> matchedResults,
            DetectItemInfo item,
            string itemName,
            NodeParamTDAI param,
            List<DetectItemInfo> deteItems,
            HashSet<string> addedRectKeys,
            List<ColorText> texts,
            List<SingleDetectResult> singleResults,
            ref NodeResultTDAI nodeResult)
        {
            float.TryParse(item.MaxValue, out float max);

            string valueStr = "NA";
            var (rectOKs, rectNGs) = SeparateOutlierRects(matchedResults, max, param, ref valueStr, singleResults, itemName);
            bool allOk = singleResults.Count > 0 && rectNGs.Count == 0;

            foreach (var rect in rectOKs)
                ParseCommon.AddRect(addedRectKeys, rect, true, ref nodeResult);

            foreach (var rect in rectNGs)
                ParseCommon.AddRect(addedRectKeys, rect, param.IsAutoStudy || allOk, ref nodeResult);

            if (singleResults.Count == 0)
            {
                singleResults.Add(new SingleDetectResult
                {
                    Name = itemName,
                    Value = valueStr,
                    IsOk = param.IsAutoStudy || allOk
                });
            }

            AddDetectText(texts, deteItems, item, itemName, param.IsAutoStudy || allOk ? "OK" : "NG", param.IsAutoStudy || allOk);
            ParseCommon.SetDetectItemCurValue(ref param, itemName, valueStr.TrimEnd(','));

            return true;
        }

        /// <summary>
        /// 添加检测项显示文本。
        /// </summary>
        /// <param name="texts">颜色文本集合。</param>
        /// <param name="deteItems">当前检测模板内所有检测项。</param>
        /// <param name="item">检测项配置。</param>
        /// <param name="itemName">检测项名称。</param>
        /// <param name="value">当前实测值。</param>
        /// <param name="isOk">检测项是否OK。</param>
        private static void AddDetectText(List<ColorText> texts, List<DetectItemInfo> deteItems, DetectItemInfo item, string itemName, string value, bool isOk)
        {
            bool isEnable = ParseCommon.GetItemEnableStatus(deteItems, itemName);
            string displayName = ParseCommon.GetDisplayName(itemName);
            if (!isEnable || texts.Any(t => t.Text.StartsWith($"{displayName}:")))
                return;

            texts.Add(new ColorText(
                $"{displayName}: {value}",
                isOk ? Color.Green : Color.Red));
        }

        /// <summary>
        /// 自动学习模式下添加实测值。
        /// </summary>
        /// <param name="itemName">检测项名称。</param>
        /// <param name="value">实测值。</param>
        /// <param name="param">AI节点参数。</param>
        private static void AddAutoStudyValue(string itemName, float value, NodeParamTDAI param)
        {
            if (!param.IsAutoStudy)
                return;

            if (!_autoStudyDatas.ContainsKey(itemName))
                _autoStudyDatas.Add(itemName, new List<float>());
            _autoStudyDatas[itemName].Add(value);
        }

        /// <summary>
        /// 自动学习样本数达到配置值后写回检测项上下限。
        /// </summary>
        /// <param name="deteItems">当前检测模板内所有检测项。</param>
        /// <param name="param">AI节点参数。</param>
        private static void FinishAutoStudyIfNeeded(List<DetectItemInfo> deteItems, NodeParamTDAI param)
        {
            if (!param.IsAutoStudy)
                return;

            _count++;
            if (param.StudyNum > _count)
                return;

            try
            {
                ParseCommon.SetDetectItemMinMax(deteItems, _autoStudyDatas, param.StudyPercentage);
                ParseCommon.CloseAutoStudyEvent?.Invoke(null, param.NodeName);
                _count = 0;
            }
            catch (Exception)
            {
                _count = 0;
                throw;
            }
        }

        /// <summary>
        /// 按桌面版本算法分离聚拢度OK和NG矩形框。
        /// </summary>
        /// <param name="results">线芯SEG结果。</param>
        /// <param name="distanceLimit">允许的中心距离上限。</param>
        /// <param name="param">AI节点参数。</param>
        /// <param name="resultValue">输出每个线芯到质心的距离字符串。</param>
        /// <param name="singleDetectResults">单检测项结果集合。</param>
        /// <param name="itemName">检测项名称。</param>
        /// <returns>OK矩形列表和NG矩形列表。</returns>
        public static (List<Rect>, List<Rect>) SeparateOutlierRects(
            List<SegResult> results,
            float distanceLimit,
            NodeParamTDAI param,
            ref string resultValue,
            List<SingleDetectResult> singleDetectResults,
            string itemName)
        {
            var inliers = new List<Rect>();
            var outliers = new List<Rect>();

            try
            {
                if (results == null || results.Count == 0)
                {
                    resultValue = "NA";
                    return (inliers, outliers);
                }

                if (results.Count == 1)
                {
                    resultValue = "NA";
                    outliers.Add(results[0].Rect);
                    return (inliers, outliers);
                }

                var centers = results.Select(r =>
                {
                    Rect rect = r.Rect;
                    return new PointF(rect.X + rect.Width / 2.0f, rect.Y + rect.Height / 2.0f);
                }).ToList();

                float centerX = centers.Average(p => p.X);
                float centerY = centers.Average(p => p.Y);
                var centroid = new PointF(centerX, centerY);

                resultValue = string.Empty;
                for (int i = 0; i < results.Count; i++)
                {
                    double distance = Distance(centers[i], centroid);
                    float displayValue = param.NeedConvert ? (float)distance * param.Scale : (float)distance;
                    string rawValue = displayValue.ToString("F2");
                    bool isOk = displayValue <= distanceLimit;

                    AddAutoStudyValue(itemName, displayValue, param);

                    if (isOk)
                        inliers.Add(results[i].Rect);
                    else
                        outliers.Add(results[i].Rect);

                    singleDetectResults.Add(new SingleDetectResult
                    {
                        Name = itemName,
                        Value = rawValue,
                        IsOk = param.IsAutoStudy || isOk
                    });

                    resultValue += rawValue + ",";
                }
            }
            catch (Exception e)
            {
                LogHelper.AddLog(MsgLevel.Exception, e.Message, true);
                resultValue = "NA";
            }

            return (inliers, outliers);
        }

        /// <summary>
        /// 计算两点间欧几里得距离。
        /// </summary>
        /// <param name="a">第一个点。</param>
        /// <param name="b">第二个点。</param>
        /// <returns>两点距离。</returns>
        private static double Distance(PointF a, PointF b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
