using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using OpenCvSharp;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._3_Detection.CombinationColor
{
    /// <summary>可替换的颜色分割和区域测量接口，运行服务不依赖窗体。</summary>
    internal interface ICombinationColorAlgorithm
    {
        /// <summary>生成由调用方释放的全尺寸颜色掩膜。</summary>
        Mat Segment(Mat bgr, NodeParamCombinationColor parameters);
        /// <summary>过滤和测量掩膜，返回由结果资源管理器接管的结果。</summary>
        NodeResultCombinationColor Analyze(Mat mask, NodeParamCombinationColor parameters);
    }

    /// <summary>组合模式算法的默认接口实现，保留纯函数供离线验证。</summary>
    internal sealed class CombinationColorEngine : ICombinationColorAlgorithm
    {
        /// <summary>执行颜色分割。</summary>
        public Mat Segment(Mat bgr, NodeParamCombinationColor parameters) => CombinationColorAlgorithm.BuildMask(bgr, parameters);
        /// <summary>执行区域筛选及测量。</summary>
        public NodeResultCombinationColor Analyze(Mat mask, NodeParamCombinationColor parameters) => CombinationColorAlgorithm.Measure(mask, parameters);
    }

    /// <summary>不依赖界面的组合颜色分割与连通区域测量服务。</summary>
    internal static class CombinationColorAlgorithm
    {
        /// <summary>沿用组合模式通道阈值、跨零色相及 5×5 闭运算。</summary>
        internal static Mat BuildMask(Mat bgr, NodeParamCombinationColor parameters)
        {
            if (bgr == null || bgr.Empty() || bgr.Type() != MatType.CV_8UC3)
                throw new ArgumentException("颜色识别需要三通道 BGR 彩色图像。");
            var learned = parameters.Models?.FirstOrDefault(m => m != null && m.ColorSpaceName == parameters.ColorSpace);
            if (learned == null) throw new InvalidOperationException("请先学习目标颜色。");
            if (parameters.MinimumArea < 0) throw new ArgumentException("最小面积不能小于零。");
            var model = GuideColorSpaceConverter.ApplySensitivity(learned, parameters.Sensitivity);
            var mask = new Mat();
            try
            {
                using (var converted = GuideColorSpaceConverter.Convert(bgr, parameters.ColorSpace))
                using (var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(5, 5)))
                {
                    if (model.ColorSpaceName == "HSV" || model.ColorSpaceName == "HSI")
                        CombinationColorLearning.BuildStep2HsvMask(converted, model, mask);
                    else Cv2.InRange(converted, new Scalar(model.Channel1Low, model.Channel2Low, model.Channel3Low),
                        new Scalar(model.Channel1High, model.Channel2High, model.Channel3High), mask);
                    Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kernel);
                }
                return mask;
            }
            catch { mask.Dispose(); throw; }
        }

        /// <summary>先按实际像素面积筛选，再选择输出模式；标签零为背景，不参与统计。</summary>
        internal static NodeResultCombinationColor Measure(Mat mask, NodeParamCombinationColor parameters)
        {
            using (var labels = new Mat())
            using (var stats = new Mat())
            using (var centers = new Mat())
            using (var selected = new Mat(mask.Size(), MatType.CV_8UC1, Scalar.All(0)))
            {
                int count = Cv2.ConnectedComponentsWithStats(mask, labels, stats, centers, PixelConnectivity.Connectivity8);
                var candidates = Enumerable.Range(1, count - 1)
                    .Where(id => stats.At<int>(id, (int)ConnectedComponentsTypes.Area) >= parameters.MinimumArea)
                    .OrderByDescending(id => stats.At<int>(id, (int)ConnectedComponentsTypes.Area)).ToList();
                if (parameters.OutputRegionMode == ColorOutputRegionMode.Max) candidates = candidates.Take(1).ToList();
                else if (parameters.OutputRegionMode != ColorOutputRegionMode.All) throw new ArgumentException("输出模式仅支持最大或全部。");
                var result = new NodeResultCombinationColor { IsOk = true, JudgeOk = true, Count = candidates.Count };
                result.Result.IsAllOk = true;
                foreach (int id in candidates)
                {
                    // 每个标签只处理自身包围框，避免区域多时反复扫描全图。
                    var bounds = new Rect(stats.At<int>(id, 0), stats.At<int>(id, 1), stats.At<int>(id, 2), stats.At<int>(id, 3));
                    using (var localLabels = new Mat(labels, bounds))
                    using (var localSelected = new Mat(selected, bounds))
                    using (var component = new Mat())
                    {
                        Cv2.InRange(localLabels, Scalar.All(id), Scalar.All(id), component);
                        Cv2.BitwiseOr(localSelected, component, localSelected);
                        Cv2.FindContours(component, out OpenCvSharp.Point[][] contours, out HierarchyIndex[] hierarchy,
                            RetrievalModes.External, ContourApproximationModes.ApproxSimple);
                        var points = contours.SelectMany(c => c).ToArray();
                        var rectangle = Cv2.MinAreaRect(points);
                        rectangle.Center = new Point2f(rectangle.Center.X + bounds.X, rectangle.Center.Y + bounds.Y);
                        double area = stats.At<int>(id, (int)ConnectedComponentsTypes.Area);
                        result.Areas.Add(area);
                        result.Lengths.Add(Math.Max(rectangle.Size.Width, rectangle.Size.Height));
                        result.Widths.Add(Math.Min(rectangle.Size.Width, rectangle.Size.Height));
                        result.Area += area;
                        result.Result.Rects.Add(new ColorRotatedRect(rectangle.Center.X, rectangle.Center.Y,
                            rectangle.Size.Width, rectangle.Size.Height, rectangle.Angle, Color.Lime));
                    }
                }
                result.Height = result.Lengths.FirstOrDefault();
                result.Width = result.Widths.FirstOrDefault();
                result.Result.Texts.Add(new ColorText($"区域个数：{result.Count} 总面积：{result.Area:F0} 像素²", Color.Lime)
                { FontSize = 14, Position = DisplayTextPosition.TopLeft, Title = "组合颜色识别" });
                var ownedMask = selected.Clone();
                result.OutputImage = OutputImage.FromOwnedSingleImage(ownedMask, ownedMask);
                result.OutputImage.DisplayResult = result.Result;
                return result;
            }
        }
    }
}
