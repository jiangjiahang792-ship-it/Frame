using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OpenCvSharp;
using Sunny.UI;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;
using RotatedRect = TDJS_Vision.Node._3_Detection.TDAI.Yolo8.RotatedRect;

namespace TDJS_Vision.Node._3_Detection.TDAI.Parse
{
    /// <summary>
    /// 解析公共类，定义一些常用的解析方法和工具函数
    /// </summary>
    public class ParseCommon
    {
        /// <summary>
        /// 关闭自动学习事件
        /// </summary>
        public static EventHandler<string> CloseAutoStudyEvent { get; set; }

        /// <summary>
        /// 不添加重复的旋转矩形框
        /// </summary>
        public static void AddRect(HashSet<string> addedRectKeys, RotatedRect rotatedRect, bool isOk, ref NodeResultTDAI nodeResult)
        {
            string rectKey = $"{rotatedRect.center.x},{rotatedRect.center.y},{rotatedRect.size.width},{rotatedRect.size.height}";
            // 添加 Rect（如果还没加过）
            if (!addedRectKeys.Contains(rectKey))
            {
                var rect = new ColorRotatedRect(rotatedRect);
                rect.AddFlag(isOk); // 初始颜色由该检测项决定
                addedRectKeys.Add(rectKey);
                nodeResult.AlgorithmResult.Rects.Add(rect);
            }
            else
            {
                // 如果已经加过，则更新颜色（只要有一个 NG 就设为红色）
                var targetRect = nodeResult.AlgorithmResult.Rects.Find(r =>
                    r.RotatedRect.center.x == rotatedRect.center.x &&
                    r.RotatedRect.center.y == rotatedRect.center.y &&
                    r.RotatedRect.size.width == rotatedRect.size.width &&
                    r.RotatedRect.size.height == rotatedRect.size.height);

                if (targetRect != default(ColorRotatedRect))
                {
                    targetRect.AddFlag(isOk); // 更新颜色依据
                }
            }
        }

        /// <summary>
        /// 不添加重复的旋转矩形框
        /// </summary>
        public static void AddRect(HashSet<string> addedRectKeys, Rect rect, bool isOk, ref NodeResultTDAI nodeResult)
        {
            string rectKey = $"{GetCenter(rect).X},{GetCenter(rect).Y},{rect.Size.Width},{rect.Size.Height}";
            // 添加 Rect（如果还没加过）
            if (!addedRectKeys.Contains(rectKey))
            {
                var rectTmp = new ColorRotatedRect(rect.Location.X, rect.Location.Y, rect.Size.Width, rect.Size.Height);
                rectTmp.AddFlag(isOk); // 初始颜色由该检测项决定
                addedRectKeys.Add(rectKey);
                nodeResult.AlgorithmResult.Rects.Add(rectTmp);
            }
            else
            {
                // 如果已经加过，则更新颜色（只要有一个 NG 就设为红色）
                var targetRect = nodeResult.AlgorithmResult.Rects.Find(r =>
                    r.RotatedRect.center.x == GetCenter(rect).X &&
                    r.RotatedRect.center.y == GetCenter(rect).Y &&
                    r.RotatedRect.size.width == rect.Width &&
                    r.RotatedRect.size.height == rect.Height);

                if (targetRect != default(ColorRotatedRect))
                {
                    targetRect.AddFlag(isOk); // 更新颜色依据
                }
            }
        }

        /// <summary>
        /// 获取矩形中心点
        /// </summary>
        /// <param name="rect"></param>
        /// <returns></returns>
        public static PointF GetCenter(Rect rect)
        {
            return new PointF(
                rect.X + rect.Width / 2.0f,
                rect.Y + rect.Height / 2.0f
            );
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
        public static void SetDetectItemCurValue(ref NodeParamTDAI param, string detectItemName, string value)
        {
            detectItemName = DetectItemLanguage.NormalizeName(detectItemName);
            var detectItem = Solution.Instance.DetectItemDic[param.CurDetectItemName].FirstOrDefault(x => x.Name == detectItemName);
            if (detectItem != null)
            {
                detectItem.CurValue = value;
            }
        }

        /// <summary>
        /// 判断检测项实测值是否落在配置上下限内。
        /// </summary>
        /// <param name="item">检测项配置。</param>
        /// <param name="value">待判断的实测值。</param>
        /// <returns>上下限均解析成功且实测值在范围内时返回 true。</returns>
        public static bool IsValueWithinLimits(DetectItemInfo item, float value)
        {
            if (item == null)
                return false;

            bool minOk = float.TryParse(item.MinValue, out float min) && value >= min;
            bool maxOk = float.TryParse(item.MaxValue, out float max) && value <= max;
            return minOk && maxOk;
        }

        public static void NormalizeDetectItems(List<DetectItemInfo> detectItemInfos)
        {
            DetectItemLanguage.NormalizeItems(detectItemInfos);
        }

        public static string GetDisplayName(string detectItemName)
        {
            return DetectItemLanguage.GetDisplayName(detectItemName);
        }

        /// <summary>
        /// 设置检测项的最小最大值
        /// </summary>
        /// <param name="nodeResult"></param>
        /// <param name="itemName"></param>
        public static void SetDetectItemMinMax(List<DetectItemInfo> detectItemInfos, Dictionary<string, List<float>> dic, float percentage)
        {
            foreach (var item in detectItemInfos)
            {
                // 检测项没启用就不用处理
                if (!item.Enable) continue;

                // 没有学习到结果的检测项也不用处理
                if (!dic.ContainsKey(item.Name)) continue;

                if (item.IsCountItem)
                {
                    // 数量型检测项，设置最大最小值为数据列表的众数
                    float res = dic[item.Name]
                    .GroupBy(x => x)
                    .OrderByDescending(g => g.Count())
                    .ThenBy(g => g.Key)
                    .First()
                    .Key;
                    var detectItem = detectItemInfos.FirstOrDefault(x => x.Name == item.Name);
                    if (detectItem != null)
                    {
                        detectItem.MinValue = ((int)res).ToString();
                        detectItem.MaxValue = ((int)res).ToString();
                    }
                }
                else
                {
                    // 非数量型检测项，设置最大最小值为数据列表的最小值往下（最大值往上）浮动一定%
                    float minV = dic[item.Name].Min() * (1 - percentage / 100);
                    float maxV = dic[item.Name].Max() * (1 + percentage / 100);
                    var detectItem = detectItemInfos.FirstOrDefault(x => x.Name == item.Name);
                    if (detectItem != null)
                    {
                        detectItem.MinValue = minV.ToString("F2");
                        detectItem.MaxValue = maxV.ToString("F2");
                    }
                }
                dic.Remove(item.Name); // 设置好后要清空对应数据列表，避免下次自动学习误用旧数据
            }
        }


        /// <summary>
        /// 设置检测项的最小最大值
        /// </summary>
        /// <param name="nodeResult"></param>
        /// <param name="itemName"></param>
        public static void SetDetectItemMinMax(List<DetectItemInfo> detectItemInfos, Dictionary<string, List<string>> dic)
        {
            foreach (var item in detectItemInfos)
            {
                // 检测项没启用就不用处理
                if (!item.Enable) continue;

                // 没有学习到结果的检测项也不用处理
                if (!dic.ContainsKey(item.Name)) continue;

                string res = dic[item.Name].First();
                var detectItem = detectItemInfos.FirstOrDefault(x => x.Name == item.Name);
                if (detectItem != null)
                {
                    detectItem.MinValue = res;
                    detectItem.MaxValue = res;
                }
                dic.Remove(item.Name); // 设置好后要清空对应数据列表，避免下次自动学习误用旧数据
            }
        }

        /// <summary>
        /// 获取检测项是否启用的状态
        /// </summary>
        public static bool GetItemEnableStatus(List<DetectItemInfo> items, string itemName)
        {
            itemName = DetectItemLanguage.NormalizeName(itemName);
            return items.FirstOrDefault(x => x.Name == itemName)?.Enable ?? false;
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
        /// 从逗号分隔的数字字符串中解析出最小值和最大值
        /// </summary>
        /// <param name="input">输入字符串，如 "2660.00,2812.00,..."</param>
        /// <returns>包含最小值和最大值的元组 (Min, Max)</returns>
        public static (double Min, double Max) GetMinAndMax(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return (-1, -1);

            try
            {
                // 去除空格、分割、解析为 double，并过滤空项
                var numbers = input
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => double.Parse(s.Trim()))
                    .ToArray();

                if (numbers.Length == 0)
                    throw new InvalidOperationException("字符串中未找到有效数字。");

                double min = numbers.Min();
                double max = numbers.Max();

                return (min, max);
            }
            catch (FormatException ex)
            {
                return (-1, -1);
            }
            catch (OverflowException ex)
            {
                return (-1, -1);
            }
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
                if (!ResMap.ContainsKey(result.class_id))
                {
                    ResMap[result.class_id] = new List<ObbResult>();
                }
                ResMap[result.class_id].Add(result);
            }
            // 使用LINQ按键class_id从小到大进行排序
            return new SortedDictionary<int, List<ObbResult>>(ResMap);
        }
        /// <summary>
        /// 归一化旋转矩形角度，并调整宽高使其符合常规定义
        /// </summary>
        /// <param name="obbResults"></param>
        public static void NormalizeObbResultsInPlace(List<ObbResult> obbResults)
        {
            if (obbResults == null) return;

            for (int i = 0; i < obbResults.Count; i++)
            {
                ObbResult obb = obbResults[i]; // 读取副本

                // 归一化角度
                float angle = obb.angle;
                while (angle > 180f) angle -= 360f;
                while (angle <= -180f) angle += 360f;

                // 判断是否接近竖直方向
                if ((angle >= 45f && angle < 135f) || (angle >= -135f && angle < -45f))
                {
                    angle -= 90f;
                    // 交换宽高
                    (obb.width, obb.height) = (obb.height, obb.width);
                }

                // 最终角度校准
                while (angle > 45f) angle -= 90f;
                while (angle <= -45f) angle += 90f;

                obb.angle = angle;

                // 写回列表（关键！）
                obbResults[i] = obb;
            }
        }
    }
}
