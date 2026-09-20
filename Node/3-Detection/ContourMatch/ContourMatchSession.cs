using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using OpenCvSharp;

namespace TDJS_Vision.Node._3_Detection.ContourMatch
{
    /// <summary>串行保护多模板缓存，并在同一搜索区域中合并启用模板的结果。</summary>
    internal sealed class ContourMatchSession : IDisposable
    {
        /// <summary>保护原生调用和释放的互斥锁。</summary>
        private readonly object _gate = new object();
        /// <summary>可替换的匹配器工厂。</summary>
        private readonly Func<IShapeMatcher> _factory;
        /// <summary>当前完整模板集合，仅在参数快照变化后替换。</summary>
        private List<CachedTemplate> _models = new List<CachedTemplate>();
        /// <summary>缓存对应的参数快照。</summary>
        private NodeParamContourMatch _modelParameters;
        /// <summary>释放标记。</summary>
        private bool _disposed;
        /// <summary>建立可替换的会话。</summary>
        internal ContourMatchSession(Func<IShapeMatcher> factory = null) { _factory = factory ?? (() => new NativeShapeMatcher()); }

        /// <summary>按区域搜索各启用模板，将坐标恢复至原图后统一排序、去重和限数。</summary>
        internal ContourMatchExecution Execute(Mat source, NodeParamContourMatch parameters, CancellationToken token)
        {
            lock (_gate)
            {
                token.ThrowIfCancellationRequested();
                if (_disposed) throw new ObjectDisposedException(nameof(ContourMatchSession));
                if (parameters == null) throw new InvalidOperationException("请先配置轮廓模板参数。");
                NativeShapeMatcher.ValidateFind(parameters.FindOptions);
                EnsureModels(parameters, token);
                var timer = Stopwatch.StartNew();
                var candidates = new List<ShapeMatchResult>();
                var regions = GetSearchRegions(parameters);
                foreach (var polygon in regions)
                {
                    Rect bounds = polygon == null ? new Rect(0, 0, source.Width, source.Height) : Cv2.BoundingRect(polygon);
                    bounds = bounds.Intersect(new Rect(0, 0, source.Width, source.Height));
                    if (bounds.Width < 8 || bounds.Height < 8) continue;
                    // ROI Mat仅建立视图，保留真实步长，不复制整幅生产图像。
                    using (var view = new Mat(source, bounds))
                    foreach (var model in _models)
                    {
                        token.ThrowIfCancellationRequested();
                        foreach (var match in model.Matcher.Find(view, parameters.FindOptions))
                        {
                            match.CenterX += bounds.X; match.CenterY += bounds.Y;
                            if (polygon != null && Cv2.PointPolygonTest(polygon, new Point2f((float)match.CenterX, (float)match.CenterY), false) < 0) continue;
                            match.TemplateId = model.Id; match.TemplateName = model.Name; match.ModelContours = model.Contours;
                            candidates.Add(match);
                        }
                    }
                }
                var accepted = new List<ShapeMatchResult>();
                foreach (var match in candidates.OrderByDescending(item => item.Score))
                {
                    if (accepted.Any(other => Overlap(match, other) > parameters.FindOptions.MaximumOverlap)) continue;
                    accepted.Add(match);
                    if (accepted.Count >= parameters.FindOptions.MaximumMatches) break;
                }
                timer.Stop(); token.ThrowIfCancellationRequested();
                return new ContourMatchExecution { Matches = accepted, Milliseconds = timer.Elapsed.TotalMilliseconds };
            }
        }

        /// <summary>整组恢复成功后才替换缓存，某个模板失败不会留下半更新的缓存。</summary>
        private void EnsureModels(NodeParamContourMatch parameters, CancellationToken token)
        {
            if (ReferenceEquals(_modelParameters, parameters)) return;
            var enabled = parameters.GetTemplates().Where(item => item.Enabled).ToList();
            if (enabled.Count == 0) throw new InvalidOperationException("请至少创建并启用一个特征模板。");
            var pending = new List<CachedTemplate>();
            try
            {
                foreach (var entry in enabled)
                {
                    token.ThrowIfCancellationRequested();
                    var matcher = Restore(entry.Model, _factory);
                    try { pending.Add(new CachedTemplate { Id = entry.Id, Name = entry.Name, Matcher = matcher, Contours = matcher.GetModelContours() }); }
                    catch { matcher.Dispose(); throw; }
                }
                token.ThrowIfCancellationRequested();
                foreach (var model in _models) model.Matcher.Dispose();
                _models = pending; pending = null; _modelParameters = parameters;
            }
            finally { if (pending != null) foreach (var model in pending) model.Matcher.Dispose(); }
        }

        /// <summary>生成输入图像中的固定搜索四边形，全图搜索使用空区域标记。</summary>
        internal static List<Point2f[]> GetSearchRegions(NodeParamContourMatch parameters)
        {
            if (parameters.AllSearch) return new List<Point2f[]> { null };
            Rectangle region = parameters.SearchRegion;
            if (region.Width < 8 || region.Height < 8) throw new InvalidOperationException("请绘制至少8×8像素的搜索区域。");
            return new List<Point2f[]> { new[] { new Point2f(region.Left, region.Top), new Point2f(region.Right, region.Top),
                new Point2f(region.Right, region.Bottom), new Point2f(region.Left, region.Bottom) } };
        }
        /// <summary>与Demo一致，以旋转框交集除以较小框面积判断重复目标。</summary>
        private static double Overlap(ShapeMatchResult first, ShapeMatchResult second)
        {
            var a = new RotatedRect(new Point2f((float)first.CenterX, (float)first.CenterY), new Size2f((float)first.Width, (float)first.Height), (float)-first.AngleDegrees);
            var b = new RotatedRect(new Point2f((float)second.CenterX, (float)second.CenterY), new Size2f((float)second.Width, (float)second.Height), (float)-second.AngleDegrees);
            Point2f[] intersection;
            double area = Cv2.IntersectConvexConvex(a.Points(), b.Points(), out intersection, true);
            return area / Math.Max(1, Math.Min(first.Width * first.Height, second.Width * second.Height));
        }

        /// <summary>重建已确认模型及逐次删除记录；失败时销毁候选，绝不替换旧模型。</summary>
        internal static IShapeMatcher Restore(NodeParamContourMatch parameters, Func<IShapeMatcher> factory = null)
        {
            if (parameters == null || parameters.ModelOptions == null || parameters.ModelImageBytes == null)
                throw new InvalidOperationException("请先创建轮廓模板。");
            IShapeMatcher candidate = (factory ?? (() => new NativeShapeMatcher()))();
            try
            {
                using (ImageFrame image = ImageFrame.Decode(parameters.ModelImageBytes))
                    TemplateRegionMask.Create(candidate, image.Mat, parameters.ModelRoi, parameters.ModelOptions, parameters.ModelRegions);
                foreach (byte[] mask in parameters.EraseMasks ?? new List<byte[]>())
                    candidate.EraseModelFeatures(mask, parameters.ModelRoi.Width, parameters.ModelRoi.Height);
                return candidate;
            }
            catch { candidate.Dispose(); throw; }
        }

        /// <summary>等待搜索结束后释放所有模板。</summary>
        public void Dispose()
        {
            lock (_gate) { _disposed = true; foreach (var model in _models) model.Matcher.Dispose(); _models.Clear(); _modelParameters = null; }
        }
        /// <summary>一个已加载的模型及不变的轮廓缓存。</summary>
        private sealed class CachedTemplate
        {
            /// <summary>模板稳定标识。</summary>
            internal string Id;
            /// <summary>结果显示名称。</summary>
            internal string Name;
            /// <summary>独占的原生实例。</summary>
            internal IShapeMatcher Matcher;
            /// <summary>绘制所需的局部轮廓。</summary>
            internal IReadOnlyList<PointF[]> Contours;
        }
    }
    /// <summary>已合并、使用原图坐标的多模板执行结果。</summary>
    internal sealed class ContourMatchExecution
    {
        /// <summary>各结果自带模板标识及局部轮廓。</summary>
        internal IReadOnlyList<ShapeMatchResult> Matches { get; set; }
        /// <summary>兼容旧单模板测试的轮廓入口。</summary>
        internal IReadOnlyList<PointF[]> Contours { get; set; } = Array.Empty<PointF[]>();
        /// <summary>搜索及结果合并耗时，不含模型恢复。</summary>
        internal double Milliseconds { get; set; }
    }
}
