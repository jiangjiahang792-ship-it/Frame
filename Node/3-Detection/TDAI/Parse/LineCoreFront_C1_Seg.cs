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
        /// <param name="imgTmp">用于调试绘制标准圆的图像，允许为空。</param>
        /// <param name="nodeResult">AI节点运行结果。</param>
        public static void Parse(List<SegResult> results, int resultCount, NodeParamTDAI param, Mat imgTmp, ref NodeResultTDAI nodeResult)
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
                    hasData = ParseGatheringItem(matchedResults, item, itemName, param, imgTmp, deteItems, addedRectKeys, texts, singleResults, ref nodeResult);
                }

                if (hasData)
                    detectResults[itemName] = singleResults;
            }

            nodeResult.AlgorithmResult.DetectResults = detectResults;
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
            string valueStr = count > 0 ? count.ToString() : "NA";

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
        /// <param name="imgTmp">用于调试绘制标准圆的图像。</param>
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
            Mat imgTmp,
            List<DetectItemInfo> deteItems,
            HashSet<string> addedRectKeys,
            List<ColorText> texts,
            List<SingleDetectResult> singleResults,
            ref NodeResultTDAI nodeResult)
        {
            float.TryParse(item.MaxValue, out float max);

            string maxValueStr = "NA";
            bool allOk = false;
            var (rectOKs, rectNGs) = GetResultMinCircle(matchedResults, max, param, imgTmp, ref maxValueStr, ref allOk, singleResults, itemName);

            foreach (var rect in rectOKs)
                ParseCommon.AddRect(addedRectKeys, rect, true, ref nodeResult);

            foreach (var rect in rectNGs)
                ParseCommon.AddRect(addedRectKeys, rect, allOk, ref nodeResult);

            if (singleResults.Count == 0)
            {
                singleResults.Add(new SingleDetectResult
                {
                    Name = itemName,
                    Value = maxValueStr,
                    IsOk = param.IsAutoStudy || allOk
                });
            }

            AddDetectText(texts, deteItems, item, itemName, maxValueStr, param.IsAutoStudy || allOk);
            ParseCommon.SetDetectItemCurValue(ref param, itemName, maxValueStr);

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
                $"{displayName}: {value} 设定值: [{item.MinValue}, {item.MaxValue}]",
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
        /// 获取所有线芯中心点的最小包围圆，并按标准半径区分OK和NG目标框。
        /// </summary>
        /// <param name="results">线芯SEG结果。</param>
        /// <param name="okRadius">标准半径，单位由节点换算配置决定。</param>
        /// <param name="param">AI节点参数。</param>
        /// <param name="mat">用于调试绘制标准圆的图像，允许为空。</param>
        /// <param name="maxV">输出最大聚拢半径。</param>
        /// <param name="isOk">输出聚拢度是否OK。</param>
        /// <param name="singleDetectResults">单检测项结果集合。</param>
        /// <param name="itemName">检测项名称。</param>
        /// <returns>OK矩形列表和NG矩形列表。</returns>
        public static (List<Rect>, List<Rect>) GetResultMinCircle(
            List<SegResult> results,
            float okRadius,
            NodeParamTDAI param,
            Mat mat,
            ref string maxV,
            ref bool isOk,
            List<SingleDetectResult> singleDetectResults,
            string itemName)
        {
            var inliers = new List<Rect>();
            var outliers = new List<Rect>();

            try
            {
                if (results == null || results.Count == 0)
                {
                    maxV = "NA";
                    isOk = false;
                    return (inliers, outliers);
                }

                if (results.Count == 1)
                {
                    maxV = "NA";
                    isOk = false;
                    outliers.Add(results[0].Rect);
                    return (inliers, outliers);
                }

                float okRadiusPixel = ConvertToPixelRadius(okRadius, param);
                var (center, radius, okRects, ngRects) = CalculateMinCircle(results, okRadiusPixel);

                DrawStandardCircle(mat, center, okRadiusPixel);

                inliers = okRects;
                outliers = ngRects;
                isOk = param.IsAutoStudy || radius <= okRadiusPixel;

                float displayRadius = param.NeedConvert ? radius * param.Scale : radius;
                maxV = displayRadius.ToString("F2");

                AddAutoStudyValue(itemName, displayRadius, param);

                singleDetectResults.Add(new SingleDetectResult
                {
                    Name = itemName,
                    Value = maxV,
                    IsOk = isOk
                });
            }
            catch (Exception e)
            {
                LogHelper.AddLog(MsgLevel.Exception, e.Message, true);
                maxV = "NA";
                isOk = false;
            }

            return (inliers, outliers);
        }

        /// <summary>
        /// 将标准半径按标定比例换算为像素值。
        /// </summary>
        /// <param name="radius">检测项配置半径。</param>
        /// <param name="param">AI节点参数。</param>
        /// <returns>像素半径。</returns>
        private static float ConvertToPixelRadius(float radius, NodeParamTDAI param)
        {
            if (param.NeedConvert && param.Scale > 0)
                return radius / param.Scale;

            return radius;
        }

        /// <summary>
        /// 在调试图像上绘制标准圆。
        /// </summary>
        /// <param name="mat">目标图像。</param>
        /// <param name="center">标准圆圆心。</param>
        /// <param name="okRadius">标准圆像素半径。</param>
        private static void DrawStandardCircle(Mat mat, PointF center, float okRadius)
        {
            if (mat == null || mat.Empty() || okRadius <= 0)
                return;

            var point = new OpenCvSharp.Point((int)Math.Round(center.X), (int)Math.Round(center.Y));
            Cv2.Circle(mat, point, (int)Math.Round(okRadius), Scalar.Blue, 3);
            Cv2.Circle(mat, point, 2, Scalar.Blue, 2);
        }

        /// <summary>
        /// 计算线芯中心点最小包围圆。
        /// </summary>
        /// <param name="results">线芯SEG结果。</param>
        /// <param name="okRadius">标准圆像素半径。</param>
        /// <returns>最小包围圆圆心、半径、OK矩形和NG矩形。</returns>
        private static (PointF Center, float Radius, List<Rect> OkRects, List<Rect> NgRects) CalculateMinCircle(List<SegResult> results, float okRadius)
        {
            var points = results.Select(r => new PointF(r.Rect.X + r.Rect.Width / 2.0f, r.Rect.Y + r.Rect.Height / 2.0f)).ToList();
            CircleInfo circle = CalculateMinimumEnclosingCircle(points);

            var okRects = new List<Rect>();
            var ngRects = new List<Rect>();
            for (int i = 0; i < results.Count; i++)
            {
                double distance = Distance(points[i], circle.Center);
                if (distance <= okRadius + 0.001)
                    okRects.Add(results[i].Rect);
                else
                    ngRects.Add(results[i].Rect);
            }

            return (circle.Center, circle.Radius, okRects, ngRects);
        }

        /// <summary>
        /// 使用确定性增量法计算点集最小包围圆。
        /// </summary>
        /// <param name="points">输入点集。</param>
        /// <returns>最小包围圆信息。</returns>
        private static CircleInfo CalculateMinimumEnclosingCircle(List<PointF> points)
        {
            CircleInfo circle = new CircleInfo(points[0], 0);

            for (int i = 0; i < points.Count; i++)
            {
                if (circle.Contains(points[i]))
                    continue;

                circle = new CircleInfo(points[i], 0);
                for (int j = 0; j < i; j++)
                {
                    if (circle.Contains(points[j]))
                        continue;

                    circle = CircleFromTwoPoints(points[i], points[j]);
                    for (int k = 0; k < j; k++)
                    {
                        if (!circle.Contains(points[k]))
                            circle = CircleFromThreePoints(points[i], points[j], points[k]);
                    }
                }
            }

            return circle;
        }

        /// <summary>
        /// 通过两个点创建包围圆。
        /// </summary>
        /// <param name="a">第一个点。</param>
        /// <param name="b">第二个点。</param>
        /// <returns>两点确定的最小圆。</returns>
        private static CircleInfo CircleFromTwoPoints(PointF a, PointF b)
        {
            var center = new PointF((a.X + b.X) / 2.0f, (a.Y + b.Y) / 2.0f);
            return new CircleInfo(center, (float)(Distance(a, b) / 2.0));
        }

        /// <summary>
        /// 通过三个点创建外接圆；三点共线时退化为最远两点圆。
        /// </summary>
        /// <param name="a">第一个点。</param>
        /// <param name="b">第二个点。</param>
        /// <param name="c">第三个点。</param>
        /// <returns>三点外接圆或退化圆。</returns>
        private static CircleInfo CircleFromThreePoints(PointF a, PointF b, PointF c)
        {
            double d = 2.0 * (a.X * (b.Y - c.Y) + b.X * (c.Y - a.Y) + c.X * (a.Y - b.Y));
            if (Math.Abs(d) < 0.000001)
                return CircleFromFarthestPair(a, b, c);

            double ux = ((a.X * a.X + a.Y * a.Y) * (b.Y - c.Y)
                + (b.X * b.X + b.Y * b.Y) * (c.Y - a.Y)
                + (c.X * c.X + c.Y * c.Y) * (a.Y - b.Y)) / d;
            double uy = ((a.X * a.X + a.Y * a.Y) * (c.X - b.X)
                + (b.X * b.X + b.Y * b.Y) * (a.X - c.X)
                + (c.X * c.X + c.Y * c.Y) * (b.X - a.X)) / d;

            var center = new PointF((float)ux, (float)uy);
            return new CircleInfo(center, (float)Distance(center, a));
        }

        /// <summary>
        /// 从三个点中选择距离最远的一对创建包围圆。
        /// </summary>
        /// <param name="a">第一个点。</param>
        /// <param name="b">第二个点。</param>
        /// <param name="c">第三个点。</param>
        /// <returns>最远两点确定的圆。</returns>
        private static CircleInfo CircleFromFarthestPair(PointF a, PointF b, PointF c)
        {
            double ab = Distance(a, b);
            double ac = Distance(a, c);
            double bc = Distance(b, c);

            if (ab >= ac && ab >= bc)
                return CircleFromTwoPoints(a, b);
            if (ac >= ab && ac >= bc)
                return CircleFromTwoPoints(a, c);

            return CircleFromTwoPoints(b, c);
        }

        /// <summary>
        /// 计算两点间距离。
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

        /// <summary>
        /// 最小包围圆的数据结构。
        /// </summary>
        private class CircleInfo
        {
            /// <summary>
            /// 圆心。
            /// </summary>
            public PointF Center { get; private set; }

            /// <summary>
            /// 半径。
            /// </summary>
            public float Radius { get; private set; }

            /// <summary>
            /// 创建最小包围圆数据。
            /// </summary>
            /// <param name="center">圆心。</param>
            /// <param name="radius">半径。</param>
            public CircleInfo(PointF center, float radius)
            {
                Center = center;
                Radius = radius;
            }

            /// <summary>
            /// 判断点是否在圆内。
            /// </summary>
            /// <param name="point">待判断点。</param>
            /// <returns>点在圆内或圆上时返回真。</returns>
            public bool Contains(PointF point)
            {
                return Distance(Center, point) <= Radius + 0.001;
            }
        }
    }
}
