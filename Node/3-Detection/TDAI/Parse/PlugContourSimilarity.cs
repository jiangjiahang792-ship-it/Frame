using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenCvSharp;

namespace TDJS_Vision.Node._3_Detection.TDAI.Parse
{
    /// <summary>可序列化的原像素轮廓点，避免保存OpenCV对象及冗余计算属性。</summary>
    public sealed class PlugShapePoint
    {
        /// <summary>相对轮廓外接框左边的像素坐标。</summary>
        public int X { get; set; }
        /// <summary>相对轮廓外接框上边的像素坐标。</summary>
        public int Y { get; set; }
    }

    /// <summary>单个正常独立区域的外轮廓模板；保存实际尺寸，不按目标分别缩放。</summary>
    public sealed class PlugShapeTemplate
    {
        /// <summary>有序闭合外轮廓点。</summary>
        public List<PlugShapePoint> Points { get; set; } = new List<PlugShapePoint>();
        /// <summary>复制全部点，保证方案编辑不会修改正在运行的模板。</summary>
        public PlugShapeTemplate Clone()
        {
            return new PlugShapeTemplate { Points = Points?.Select(p => new PlugShapePoint { X = p.X, Y = p.Y }).ToList() };
        }
    }

    /// <summary>可替换的轮廓匹配接口，结果为对齐后的区域交并比百分数。</summary>
    public interface IPlugContourSimilarity
    {
        /// <summary>记录一帧正常目标中的每个独立区域。</summary>
        List<PlugShapeTemplate> Learn(List<PlugPerimeterRegion> regions);
        /// <summary>逐目标选取最相似的正常模板；只允许位置、角度对齐，不做形变匹配。</summary>
        double Compare(Point[] contour, List<PlugShapeTemplate> templates);
        /// <summary>在同一个模板上计算整体及局部综合分数；minimumSimilarity保留接口兼容，不参与计分。</summary>
        PlugContourAssessment Evaluate(Point[] contour, PlugColorModel model, double minimumSimilarity);
        /// <summary>追加已确认正常的学习帧，重复轮廓去重，超限明确拒绝。</summary>
        void AddTemplates(List<PlugShapeTemplate> templates, List<PlugPerimeterRegion> regions);
    }

    /// <summary>单区域轮廓诊断；面积、深度和跨度均使用输入图像像素单位。</summary>
    public sealed class PlugContourAssessment
    {
        /// <summary>整体相似度乘以局部完整度后的综合评分，范围0～100。</summary>
        public double Similarity { get; set; }
        /// <summary>允许边缘偏差后的双向区域相似度，供扣分诊断使用。</summary>
        public double OverallSimilarity { get; set; }
        /// <summary>最严重连续缺失区域的归一化严重度，1表示达到三个局部门槛。</summary>
        public double LocalSeverity { get; set; }
        /// <summary>最严重连续缺失区域在容差带之外的面积。</summary>
        public int MissingArea { get; set; }
        /// <summary>该缺失区域相对待测区域的最大距离。</summary>
        public double MissingDepth { get; set; }
        /// <summary>该连续缺失区域外接框的最大跨度。</summary>
        public int MissingSpan { get; set; }
        /// <summary>是否存在同时超过面积、深度和跨度门槛的连续缺口。</summary>
        public bool HasLocalDefect { get; set; }
    }

    /// <summary>粗到细刚性对齐；缩小搜索图只用于提速，最终在共同原像素尺度上计算IoU。</summary>
    public sealed class PlugContourSimilarity : IPlugContourSimilarity
    {
        /// <summary>单轮学习模板上限，限制异常多区域导致的运行开销。</summary>
        private const int MaximumTemplates = 32;

        /// <summary>仅学习模式调用；检验整批容量后提交，正常检测不会扩充模板。</summary>
        public void AddTemplates(List<PlugShapeTemplate> templates, List<PlugPerimeterRegion> regions)
        {
            var added = Learn(regions);
            var combined = templates.Concat(added).GroupBy(t => string.Join(";", t.Points.Select(p => p.X + "," + p.Y)))
                .Select(g => g.First()).ToList();
            if (combined.Count > MaximumTemplates)
                throw new InvalidOperationException("正常轮廓模板超过32个，请减少本轮学习样本；本轮学习未保存。");
            templates.Clear();
            templates.AddRange(combined);
        }

        /// <summary>选择综合评分最高的单个正常模板，局部缺口不再单独否决。</summary>
        public PlugContourAssessment Evaluate(Point[] contour, PlugColorModel model, double minimumSimilarity)
        {
            if (model == null || model.EdgeTolerancePixels < 0 || model.EdgeTolerancePixels > 20 ||
                model.LocalMissingArea < 1 || model.LocalMissingDepth <= model.EdgeTolerancePixels ||
                model.LocalMissingDepth > 8192 || model.LocalMissingSpan < 1 ||
                double.IsNaN(model.LocalMissingDepth) || double.IsInfinity(model.LocalMissingDepth))
                throw new InvalidOperationException("局部轮廓参数无效：缺口深度必须大于边缘容差，面积和跨度必须为正数。");
            // 共用原匹配入口的模板校验，空轮廓仍返回不合格而非伪造正常值。
            ValidateTemplates(model.ShapeTemplates);
            if (contour == null || contour.Length < 3) return new PlugContourAssessment { HasLocalDefect = true };
            Point[] normalized = Normalize(contour);
            var references = model.ShapeTemplates.Select(t => t.Points.Select(p => new Point(p.X, p.Y)).ToArray()).ToArray();
            // 先扫描全部精确匹配，避免第3个学习模板能直接命中却先计算前两个昂贵的对齐。
            if (references.Any(r => r.SequenceEqual(normalized)))
                return new PlugContourAssessment { Similarity = 100, OverallSimilarity = 100 };
            var assessments = new PlugContourAssessment[references.Length];
            if (references.Length == 1)
                assessments[0] = MatchAssessment(references[0], normalized, model);
            else
            {
                // 各模板拥有独立Mat，不共享可写图像；最多使用两个任务，避免按模板数无界占用CPU。
                Parallel.For(0, references.Length, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(2, Math.Max(1, Environment.ProcessorCount / 2)) },
                    i => assessments[i] = MatchAssessment(references[i], normalized, model));
            }
            // 按模板原顺序稳定选择，平分时诊断结果与串行实现一致。
            return assessments.OrderByDescending(a => a.Similarity).First();
        }

        /// <summary>拒绝损坏或过大的持久化模板，防止异常内存分配。</summary>
        private static void ValidateTemplates(List<PlugShapeTemplate> templates)
        {
            if (templates == null || templates.Count == 0 || templates.Count > MaximumTemplates)
                throw new InvalidOperationException("缺少有效防水栓轮廓模板，请重新一键学习。");
            if (templates.Any(t => t?.Points == null || t.Points.Count < 3 || t.Points.Count > 100000 ||
                t.Points.Any(p => p == null || p.X < 0 || p.Y < 0 || p.X > 8192 || p.Y > 8192)))
                throw new InvalidOperationException("防水栓轮廓模板无效，请重新一键学习。");
        }

        /// <summary>保存相对坐标轮廓，同一轮各独立区域不合并。</summary>
        public List<PlugShapeTemplate> Learn(List<PlugPerimeterRegion> regions)
        {
            var valid = regions.Where(r => r.Contour != null && r.Contour.Length >= 3).ToList();
            if (valid.Count == 0 || valid.Count > MaximumTemplates)
                throw new InvalidOperationException("轮廓学习需要1～32个有效独立区域。");
            return valid.Select(r => new PlugShapeTemplate { Points = Normalize(r.Contour)
                .Select(p => new PlugShapePoint { X = p.X, Y = p.Y }).ToList() }).ToList();
        }

        /// <summary>将每个模板与当前区域独立对齐，返回最佳相似度。</summary>
        public double Compare(Point[] contour, List<PlugShapeTemplate> templates)
        {
            if (templates == null || templates.Count == 0 || templates.Count > MaximumTemplates)
                throw new InvalidOperationException("缺少有效防水栓轮廓模板，请重新一键学习。");
            if (contour == null || contour.Length < 3) return 0;
            Point[] current = Normalize(contour);
            double best = 0;
            foreach (var template in templates)
            {
                if (template?.Points == null || template.Points.Count < 3 || template.Points.Count > 100000 ||
                    template.Points.Any(p => p == null || p.X < 0 || p.Y < 0 || p.X > 8192 || p.Y > 8192))
                    throw new InvalidOperationException("防水栓轮廓模板无效，请重新一键学习。");
                var reference = template.Points.Select(p => new Point(p.X, p.Y)).ToArray();
                if (reference.SequenceEqual(current)) return 100;
                best = Math.Max(best, Match(reference, current));
                if (best >= 99.99999) break;
            }
            return best;
        }

        /// <summary>移除图像位置，保持轮廓形状与像素尺寸。</summary>
        private static Point[] Normalize(Point[] points)
        {
            Rect box = Cv2.BoundingRect(points);
            return points.Select(p => new Point(p.X - box.X, p.Y - box.Y)).ToArray();
        }

        /// <summary>从填实外轮廓计算中心；内部裂纹不属于本相似度指标。</summary>
        private static Point2f Center(Mat mask)
        {
            Moments moments = Cv2.Moments(mask, true);
            if (moments.M00 <= 0) throw new InvalidOperationException("防水栓轮廓面积为零。");
            return new Point2f((float)(moments.M10 / moments.M00), (float)(moments.M01 / moments.M00));
        }

        /// <summary>构建有充足旋转留白的共同画布，避免裁剪旋转后的轮廓。</summary>
        private static Mat Render(Point[] points, int size, double scale)
        {
            var mask = new Mat(size, size, MatType.CV_8UC1, Scalar.Black);
            int left = (size - (int)Math.Ceiling((points.Max(p => p.X) + 1) * scale)) / 2;
            int top = (size - (int)Math.Ceiling((points.Max(p => p.Y) + 1) * scale)) / 2;
            var scaled = points.Select(p => new Point((int)Math.Round(p.X * scale) + left,
                (int)Math.Round(p.Y * scale) + top)).ToArray();
            Cv2.FillPoly(mask, new[] { scaled }, Scalar.White);
            return mask;
        }

        /// <summary>公共尺度搜索，随后用原尺寸重新计分；不改变当前目标与模板的大小比例。</summary>
        private static double Match(Point[] reference, Point[] current)
        {
            return MatchAssessment(reference, current, null).Similarity;
        }

        /// <summary>仍按原始IoU寻找刚性对齐，仅在最终位置计算容差和局部缺口，不修改提取轮廓。</summary>
        private static PlugContourAssessment MatchAssessment(Point[] reference, Point[] current, PlugColorModel model)
        {
            int extent = Math.Max(Math.Max(reference.Max(p => p.X), reference.Max(p => p.Y)),
                Math.Max(current.Max(p => p.X), current.Max(p => p.Y))) + 1;
            if (extent > 8192) throw new InvalidOperationException("防水栓轮廓尺寸超出支持范围。");
            double scale = Math.Min(1, 128.0 / extent);
            int size = (int)Math.Ceiling(extent * scale * 1.5) + 32;
            double angle = 0, x = 0, y = 0;
            using (var a = Render(reference, size, scale))
            using (var b = Render(current, size, scale))
            {
                Search(a, b, -10, 10, 2, 0, 0, 4, 1, ref angle, ref x, ref y);
                double coarseAngle = angle, coarseX = x, coarseY = y;
                Search(a, b, Math.Max(-10, coarseAngle - 2), Math.Min(10, coarseAngle + 2), .5,
                    coarseX, coarseY, 1, .5, ref angle, ref x, ref y);
            }
            // 原尺寸上只细化候选附近，避免每帧数万次全尺寸旋转。
            int fullSize = (int)Math.Ceiling(extent * 1.5) + 32;
            using (var a = Render(reference, fullSize, 1))
            using (var b = Render(current, fullSize, 1))
            {
                double candidateAngle = angle, candidateX = x / scale, candidateY = y / scale;
                double score = Search(a, b, Math.Max(-10, candidateAngle - .5), Math.Min(10, candidateAngle + .5), .5,
                    candidateX, candidateY, Math.Max(1, (int)Math.Ceiling(.5 / scale)), 1, ref angle, ref x, ref y);
                if (model == null) return new PlugContourAssessment { Similarity = score };
                Point2f ca = Center(a), cb = Center(b);
                using (var transform = Cv2.GetRotationMatrix2D(cb, angle, 1))
                using (var aligned = new Mat())
                {
                    transform.Set<double>(0, 2, transform.At<double>(0, 2) + ca.X - cb.X + x);
                    transform.Set<double>(1, 2, transform.At<double>(1, 2) + ca.Y - cb.Y + y);
                    Cv2.WarpAffine(b, aligned, transform, a.Size(), InterpolationFlags.Nearest, BorderTypes.Constant, Scalar.Black);
                    return AssessAligned(a, aligned, model);
                }
            }
        }

        /// <summary>距离容差只作用于差异评分，不做闭运算；局部严重度参与综合评分，避免大区域稀释缺口。</summary>
        private static PlugContourAssessment AssessAligned(Mat reference, Mat current, PlugColorModel model)
        {
            using (var inverse = new Mat())
            using (var toCurrent = new Mat())
            using (var toReference = new Mat())
            using (var missing = new Mat(reference.Size(), MatType.CV_8UC1, Scalar.Black))
            using (var labels = new Mat())
            using (var stats = new Mat())
            using (var centers = new Mat())
            using (var threshold = new Mat())
            using (var excess = new Mat())
            using (var component = new Mat())
            {
                Cv2.BitwiseNot(current, inverse);
                Cv2.DistanceTransform(inverse, toCurrent, DistanceTypes.L2, DistanceTransformMasks.Mask5);
                Cv2.BitwiseNot(reference, inverse);
                Cv2.DistanceTransform(inverse, toReference, DistanceTypes.L2, DistanceTransformMasks.Mask5);
                // 批量原生运算避免逐像素跨托管边界；容差掩膜仅用于统计，绝不回写原轮廓。
                Cv2.Threshold(toCurrent, threshold, model.EdgeTolerancePixels, 255, ThresholdTypes.Binary);
                threshold.ConvertTo(missing, MatType.CV_8UC1);
                Cv2.BitwiseAnd(reference, missing, missing);
                Cv2.Threshold(toReference, threshold, model.EdgeTolerancePixels, 255, ThresholdTypes.Binary);
                threshold.ConvertTo(excess, MatType.CV_8UC1);
                Cv2.BitwiseAnd(current, excess, excess);
                int differences = Cv2.CountNonZero(missing) + Cv2.CountNonZero(excess);
                var result = new PlugContourAssessment { Similarity = Math.Max(0, 100.0 * (1 -
                    (double)differences / Math.Max(1, Cv2.CountNonZero(reference) + Cv2.CountNonZero(current)))) };
                result.OverallSimilarity = result.Similarity;
                int count = Cv2.ConnectedComponentsWithStats(missing, labels, stats, centers);
                for (int id = 1; id < count; id++)
                {
                    int area = stats.At<int>(id, (int)ConnectedComponentsTypes.Area);
                    int span = Math.Max(stats.At<int>(id, (int)ConnectedComponentsTypes.Width),
                        stats.At<int>(id, (int)ConnectedComponentsTypes.Height));
                    double minimum, depth;
                    Point minimumPoint, maximumPoint;
                    // 该连通域之外不可能贡献深度，限制到其原像素外接框，保持统计值完全一致。
                    var bounds = new Rect(stats.At<int>(id, (int)ConnectedComponentsTypes.Left),
                        stats.At<int>(id, (int)ConnectedComponentsTypes.Top),
                        stats.At<int>(id, (int)ConnectedComponentsTypes.Width),
                        stats.At<int>(id, (int)ConnectedComponentsTypes.Height));
                    using (var localLabels = new Mat(labels, bounds))
                    using (var localDepth = new Mat(toCurrent, bounds))
                    {
                        Cv2.InRange(localLabels, Scalar.All(id), Scalar.All(id), component);
                        Cv2.MinMaxLoc(localDepth, out minimum, out depth, out minimumPoint, out maximumPoint, component);
                    }
                    bool defect = area >= model.LocalMissingArea && depth >= model.LocalMissingDepth && span >= model.LocalMissingSpan;
                    // 三项取最小比例保留联合约束；深度先扣除边缘容差，微小边缘误差不会突然大幅扣分。
                    double severity = Math.Min((double)area / model.LocalMissingArea,
                        Math.Min(Math.Max(0, depth - model.EdgeTolerancePixels) /
                            (model.LocalMissingDepth - model.EdgeTolerancePixels), (double)span / model.LocalMissingSpan));
                    if (severity > result.LocalSeverity)
                    {
                        result.LocalSeverity = severity;
                        result.MissingArea = area; result.MissingDepth = depth; result.MissingSpan = span;
                    }
                    result.HasLocalDefect |= defect;
                }
                // 连续、与用户公差无关的评分：严重度0不扣分，1时保留50%，越严重得分越低。
                // 使用最严重的一个连通缺口，避免大量细碎噪声累加扣分；不增加图像遍历。
                result.Similarity = result.OverallSimilarity / (1 + result.LocalSeverity * result.LocalSeverity);
                return result;
            }
        }

        /// <summary>在给定角度和平移窗口内最大化IoU，像素统计使用原生OpenCV操作。</summary>
        private static double Search(Mat a, Mat b, double angleMin, double angleMax, double angleStep,
            double originX, double originY, int radius, double step, ref double bestAngle, ref double bestX, ref double bestY)
        {
            Point2f ca = Center(a), cb = Center(b);
            int areaA = Cv2.CountNonZero(a);
            Rect boundsA = Cv2.BoundingRect(a);
            double best = -1;
            using (var shifted = new Mat())
            using (var intersection = new Mat())
            {
                for (double angle = angleMin; angle <= angleMax + .0001; angle += angleStep)
                using (var transform = Cv2.GetRotationMatrix2D(cb, angle, 1))
                {
                    double baseX = transform.At<double>(0, 2) + ca.X - cb.X;
                    double baseY = transform.At<double>(1, 2) + ca.Y - cb.Y;
                    transform.Set<double>(0, 2, baseX); transform.Set<double>(1, 2, baseY);
                    Cv2.WarpAffine(b, shifted, transform, a.Size(), InterpolationFlags.Nearest, BorderTypes.Constant, Scalar.Black);
                    int movedArea = Cv2.CountNonZero(shifted);
                    Rect boundsB = Cv2.BoundingRect(shifted);
                    var visited = new HashSet<long>();
                    for (double y = originY - radius; y <= originY + radius + .0001; y += step)
                        for (double x = originX - radius; x <= originX + radius + .0001; x += step)
                        {
                            int dx = (int)Math.Round(x), dy = (int)Math.Round(y);
                            long key = ((long)dx << 32) ^ (uint)dy;
                            if (!visited.Add(key)) continue;
                            int left = Math.Max(boundsA.Left, boundsB.Left + dx);
                            int top = Math.Max(boundsA.Top, boundsB.Top + dy);
                            int width = Math.Min(boundsA.Right, boundsB.Right + dx) - left;
                            int height = Math.Min(boundsA.Bottom, boundsB.Bottom + dy) - top;
                            int overlap = 0;
                            // 两个非零外接框之外交集必为零，不必扫描整幅旋转留白画布。
                            if (width > 0 && height > 0)
                            using (var target = new Mat(a, new Rect(left, top, width, height)))
                            using (var source = new Mat(shifted, new Rect(left - dx, top - dy, width, height)))
                            {
                                Cv2.BitwiseAnd(target, source, intersection);
                                overlap = Cv2.CountNonZero(intersection);
                            }
                            double score = 100.0 * overlap / Math.Max(1, areaA + movedArea - overlap);
                            if (score > best || (Math.Abs(score - best) < .000001 && Math.Abs(angle) < Math.Abs(bestAngle)))
                            { best = score; bestAngle = angle; bestX = dx; bestY = dy; }
                        }
                }
            }
            return best;
        }

    }
}
