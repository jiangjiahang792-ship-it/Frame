using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw2
{
    /// <summary>
    /// 定义一个可插拔的ROI几何适配器，把订阅值追加到统一算法显示结果。
    /// </summary>
    internal interface IOverlayGeometryAdapter
    {
        /// <summary>
        /// 判断当前适配器能否处理指定值和订阅类别。
        /// </summary>
        /// <param name="value">订阅读取到的实际值。</param>
        /// <param name="category">订阅输出的数据类别。</param>
        /// <returns>可以处理返回 true。</returns>
        bool CanHandle(object value, SubscriptionDataCategory category);

        /// <summary>
        /// 把值中的几何内容追加到目标显示结果。
        /// </summary>
        /// <param name="target">目标显示结果。</param>
        /// <param name="value">订阅读取到的实际值。</param>
        /// <param name="sourceResult">订阅来源节点的完整结果。</param>
        /// <param name="category">订阅输出的数据类别。</param>
        /// <param name="context">颜色和线宽绘制上下文。</param>
        /// <param name="addedCount">实际追加的逻辑几何数量。</param>
        /// <returns>成功识别值类型返回 true。</returns>
        bool TryAppend(
            AlgorithmResult target,
            object value,
            INodeResult sourceResult,
            SubscriptionDataCategory category,
            OverlayGeometryRenderContext context,
            out int addedCount);
    }

    /// <summary>
    /// 保存一次自动ROI绘制使用的颜色、线宽和点标记参数。
    /// </summary>
    internal sealed class OverlayGeometryRenderContext
    {
        /// <summary>
        /// 获取或设置来源元素没有有效颜色时使用的默认颜色。
        /// </summary>
        public Color FallbackColor { get; set; } = Color.Lime;

        /// <summary>
        /// 获取或设置强制覆盖全部来源元素的颜色；为空时保留来源颜色。
        /// </summary>
        public Color? OverrideColor { get; set; }

        /// <summary>
        /// 获取或设置统一绘制线宽。
        /// </summary>
        public float LineWidth { get; set; } = 2F;

        /// <summary>
        /// 获取或设置单点十字标记的半边长度。
        /// </summary>
        public float PointMarkerHalfSize { get; set; } = 4F;

        /// <summary>
        /// 根据覆盖策略解析最终显示颜色。
        /// </summary>
        /// <param name="sourceColor">来源元素颜色。</param>
        /// <returns>最终显示颜色。</returns>
        public Color ResolveColor(Color sourceColor)
        {
            if (OverrideColor.HasValue)
                return OverrideColor.Value;
            return sourceColor.IsEmpty ? FallbackColor : sourceColor;
        }

        /// <summary>
        /// 获取经过下限保护的显示线宽。
        /// </summary>
        /// <returns>至少为1的线宽。</returns>
        public float ResolveLineWidth()
        {
            return Math.Max(1F, LineWidth);
        }
    }

    /// <summary>
    /// 统一注册并分派ROI几何适配器，避免参数窗体依赖具体几何类型。
    /// </summary>
    internal static class OverlayGeometryAdapterRegistry
    {
        /// <summary>
        /// 按优先级排列的内置几何适配器。
        /// </summary>
        private static readonly IReadOnlyList<IOverlayGeometryAdapter> Adapters =
            new IOverlayGeometryAdapter[]
            {
                AlgorithmResultGeometryAdapter.Instance,
                new MeasurementResultGeometryAdapter(),
                new PrimitiveGeometryAdapter(),
                new EnumerableGeometryAdapter()
            };

        /// <summary>
        /// 按实际类型缓存不依赖业务类别的适配器分派结果。
        /// </summary>
        private static readonly ConcurrentDictionary<Type, IOverlayGeometryAdapter> AdapterCache =
            new ConcurrentDictionary<Type, IOverlayGeometryAdapter>();

        /// <summary>
        /// 按结果类型缓存公开AlgorithmResult属性读取器，避免逐帧重复反射。
        /// </summary>
        private static readonly ConcurrentDictionary<Type, Func<object, AlgorithmResult>> AlgorithmResultExtractorCache =
            new ConcurrentDictionary<Type, Func<object, AlgorithmResult>>();

        /// <summary>
        /// 把订阅值中的几何内容追加到目标显示结果。
        /// </summary>
        /// <param name="target">目标显示结果。</param>
        /// <param name="value">订阅读取到的实际值。</param>
        /// <param name="sourceResult">订阅来源节点的完整结果。</param>
        /// <param name="category">订阅输出的数据类别。</param>
        /// <param name="context">颜色和线宽绘制上下文。</param>
        /// <param name="addedCount">实际追加的逻辑几何数量。</param>
        /// <returns>成功识别值类型返回 true。</returns>
        internal static bool TryAppend(
            AlgorithmResult target,
            object value,
            INodeResult sourceResult,
            SubscriptionDataCategory category,
            OverlayGeometryRenderContext context,
            out int addedCount)
        {
            addedCount = 0;
            if (target == null || value == null)
                return false;

            OverlayGeometryRenderContext effectiveContext = context ?? new OverlayGeometryRenderContext();
            IOverlayGeometryAdapter adapter = ResolveAdapter(value, category);
            return adapter != null &&
                adapter.TryAppend(target, value, sourceResult, category, effectiveContext, out addedCount);
        }

        /// <summary>
        /// 按实际类型与订阅类别查找最合适的适配器。
        /// </summary>
        /// <param name="value">订阅读取到的实际值。</param>
        /// <param name="category">订阅输出的数据类别。</param>
        /// <returns>匹配的适配器；没有匹配时返回 null。</returns>
        private static IOverlayGeometryAdapter ResolveAdapter(object value, SubscriptionDataCategory category)
        {
            Type valueType = value.GetType();
            if (!IsCategorySensitive(category))
            {
                IOverlayGeometryAdapter cached = AdapterCache.GetOrAdd(
                    valueType,
                    type => Adapters.FirstOrDefault(item => item.CanHandle(value, category)));
                if (cached != null && cached.CanHandle(value, category))
                    return cached;
            }

            return Adapters.FirstOrDefault(item => item.CanHandle(value, category));
        }

        /// <summary>
        /// 判断同一个CLR类型是否会因点集、轮廓或测量语义选择不同适配器。
        /// </summary>
        /// <param name="category">订阅输出的数据类别。</param>
        /// <returns>依赖类别语义时返回 true。</returns>
        private static bool IsCategorySensitive(SubscriptionDataCategory category)
        {
            return category == SubscriptionDataCategory.PointCollection ||
                category == SubscriptionDataCategory.Contour ||
                category == SubscriptionDataCategory.Region ||
                category == SubscriptionDataCategory.MeasurementResult;
        }

        /// <summary>
        /// 从对象本身或其公开属性中读取完整算法结果。
        /// </summary>
        /// <param name="source">待读取对象。</param>
        /// <returns>算法结果；不存在时返回 null。</returns>
        internal static AlgorithmResult TryExtractAlgorithmResult(object source)
        {
            if (source == null)
                return null;

            AlgorithmResult direct = source as AlgorithmResult;
            if (direct != null)
                return direct;

            Func<object, AlgorithmResult> extractor = AlgorithmResultExtractorCache.GetOrAdd(
                source.GetType(),
                CreateAlgorithmResultExtractor);
            return extractor(source);
        }

        /// <summary>
        /// 为指定结果类型创建公开AlgorithmResult属性读取器。
        /// </summary>
        /// <param name="sourceType">结果类型。</param>
        /// <returns>无重复反射的属性读取委托。</returns>
        private static Func<object, AlgorithmResult> CreateAlgorithmResultExtractor(Type sourceType)
        {
            PropertyInfo property = sourceType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(item => item.CanRead &&
                    item.GetIndexParameters().Length == 0 &&
                    typeof(AlgorithmResult).IsAssignableFrom(item.PropertyType));

            if (property == null)
                return source => null;

            return source => property.GetValue(source, null) as AlgorithmResult;
        }

        /// <summary>
        /// 把一个点追加为十字标记。
        /// </summary>
        /// <param name="target">目标显示结果。</param>
        /// <param name="point">点坐标。</param>
        /// <param name="context">绘制上下文。</param>
        internal static void AppendPointMarker(
            AlgorithmResult target,
            PointF point,
            OverlayGeometryRenderContext context)
        {
            float halfSize = Math.Max(1F, context.PointMarkerHalfSize);
            Color color = context.ResolveColor(Color.Empty);
            float lineWidth = context.ResolveLineWidth();
            target.Lines.Add(new ColorLine(
                new PointF(point.X - halfSize, point.Y),
                new PointF(point.X + halfSize, point.Y),
                color)
            {
                LineWidth = lineWidth
            });
            target.Lines.Add(new ColorLine(
                new PointF(point.X, point.Y - halfSize),
                new PointF(point.X, point.Y + halfSize),
                color)
            {
                LineWidth = lineWidth
            });
        }
    }

    /// <summary>
    /// 复制完整AlgorithmResult中的全部几何元素，但明确排除文本。
    /// </summary>
    internal sealed class AlgorithmResultGeometryAdapter : IOverlayGeometryAdapter
    {
        /// <summary>
        /// 获取无状态算法结果适配器的共享实例，避免逐帧重复分配对象。
        /// </summary>
        internal static readonly AlgorithmResultGeometryAdapter Instance =
            new AlgorithmResultGeometryAdapter();

        /// <summary>
        /// 初始化算法结果适配器；实例统一由 <see cref="Instance"/> 复用。
        /// </summary>
        private AlgorithmResultGeometryAdapter()
        {
        }

        /// <inheritdoc />
        public bool CanHandle(object value, SubscriptionDataCategory category)
        {
            return value is AlgorithmResult || category == SubscriptionDataCategory.AlgorithmResult;
        }

        /// <inheritdoc />
        public bool TryAppend(
            AlgorithmResult target,
            object value,
            INodeResult sourceResult,
            SubscriptionDataCategory category,
            OverlayGeometryRenderContext context,
            out int addedCount)
        {
            addedCount = 0;
            AlgorithmResult source = OverlayGeometryAdapterRegistry.TryExtractAlgorithmResult(value);
            if (source == null)
                source = OverlayGeometryAdapterRegistry.TryExtractAlgorithmResult(sourceResult);
            if (source == null)
                return false;

            float lineWidth = context.ResolveLineWidth();
            foreach (ColorRotatedRect rect in source.Rects ?? Enumerable.Empty<ColorRotatedRect>())
            {
                target.Rects.Add(new ColorRotatedRect(rect.RotatedRect)
                {
                    Color = context.ResolveColor(rect.Color),
                    LineWidth = lineWidth
                });
                addedCount++;
            }

            if (source.RectsNgMap != null)
            {
                foreach (List<ColorRotatedRect> rects in source.RectsNgMap.Values)
                {
                    foreach (ColorRotatedRect rect in rects ?? Enumerable.Empty<ColorRotatedRect>())
                    {
                        target.Rects.Add(new ColorRotatedRect(rect.RotatedRect)
                        {
                            Color = context.ResolveColor(rect.Color),
                            LineWidth = lineWidth
                        });
                        addedCount++;
                    }
                }
            }

            foreach (ColorLine line in source.Lines ?? Enumerable.Empty<ColorLine>())
            {
                target.Lines.Add(new ColorLine(line.P1, line.P2, context.ResolveColor(line.Color))
                {
                    LineWidth = lineWidth,
                    ShowCenterCross = line.ShowCenterCross
                });
                addedCount++;
            }

            foreach (ColorCircle circle in source.Circles ?? Enumerable.Empty<ColorCircle>())
            {
                target.Circles.Add(new ColorCircle(circle.Center, (int)Math.Round(circle.Radius), context.ResolveColor(circle.Color))
                {
                    Radius = circle.Radius,
                    LineWidth = lineWidth
                });
                addedCount++;
            }

            foreach (ColorArc arc in source.Arcs ?? Enumerable.Empty<ColorArc>())
            {
                target.Arcs.Add(new ColorArc(
                    arc.Center,
                    arc.Radius,
                    arc.StartAngle,
                    arc.SweepAngle,
                    context.ResolveColor(arc.Color))
                {
                    LineWidth = lineWidth
                });
                addedCount++;
            }

            foreach (ColorEllipse ellipse in source.Ellipses ?? Enumerable.Empty<ColorEllipse>())
            {
                target.Ellipses.Add(new ColorEllipse(
                    ellipse.Center,
                    ellipse.Width,
                    ellipse.Height,
                    ellipse.Angle,
                    context.ResolveColor(ellipse.Color))
                {
                    LineWidth = lineWidth
                });
                addedCount++;
            }

            foreach (ColorContour contour in source.Contours ?? Enumerable.Empty<ColorContour>())
            {
                target.Contours.Add(new ColorContour(
                    contour.Points == null ? new List<PointF>() : contour.Points.ToList(),
                    context.ResolveColor(contour.Color))
                {
                    LineWidth = lineWidth
                });
                addedCount++;
            }

            return true;
        }
    }

    /// <summary>
    /// 从测量结果来源节点的统一AlgorithmResult中提取多目标几何内容。
    /// </summary>
    internal sealed class MeasurementResultGeometryAdapter : IOverlayGeometryAdapter
    {
        /// <inheritdoc />
        public bool CanHandle(object value, SubscriptionDataCategory category)
        {
            return category == SubscriptionDataCategory.MeasurementResult;
        }

        /// <inheritdoc />
        public bool TryAppend(
            AlgorithmResult target,
            object value,
            INodeResult sourceResult,
            SubscriptionDataCategory category,
            OverlayGeometryRenderContext context,
            out int addedCount)
        {
            AlgorithmResult source = OverlayGeometryAdapterRegistry.TryExtractAlgorithmResult(value) ??
                OverlayGeometryAdapterRegistry.TryExtractAlgorithmResult(sourceResult);
            if (source == null)
            {
                addedCount = 0;
                return false;
            }

            return AlgorithmResultGeometryAdapter.Instance.TryAppend(
                target,
                source,
                sourceResult,
                SubscriptionDataCategory.AlgorithmResult,
                context,
                out addedCount);
        }
    }

    /// <summary>
    /// 处理单个带颜色几何对象、测量线和二维点。
    /// </summary>
    internal sealed class PrimitiveGeometryAdapter : IOverlayGeometryAdapter
    {
        /// <inheritdoc />
        public bool CanHandle(object value, SubscriptionDataCategory category)
        {
            return value is ColorRotatedRect ||
                value is ColorLine ||
                value is ColorCircle ||
                value is ColorArc ||
                value is ColorEllipse ||
                value is ColorContour ||
                value is MeasuredLine ||
                value is PointF ||
                value is Point ||
                value is Rectangle ||
                value is RectangleF ||
                value is OpenCvSharp.Rect ||
                value is OpenCvSharp.RotatedRect ||
                value is OpenCvSharp.LineSegmentPoint ||
                value is OpenCvSharp.CircleSegment;
        }

        /// <inheritdoc />
        public bool TryAppend(
            AlgorithmResult target,
            object value,
            INodeResult sourceResult,
            SubscriptionDataCategory category,
            OverlayGeometryRenderContext context,
            out int addedCount)
        {
            addedCount = 1;
            float lineWidth = context.ResolveLineWidth();

            ColorRotatedRect rect = value as ColorRotatedRect;
            if (rect != null)
            {
                target.Rects.Add(new ColorRotatedRect(rect.RotatedRect)
                {
                    Color = context.ResolveColor(rect.Color),
                    LineWidth = lineWidth
                });
                return true;
            }

            ColorLine line = value as ColorLine;
            if (line != null)
            {
                target.Lines.Add(new ColorLine(line.P1, line.P2, context.ResolveColor(line.Color))
                {
                    LineWidth = lineWidth,
                    ShowCenterCross = line.ShowCenterCross
                });
                return true;
            }

            ColorCircle circle = value as ColorCircle;
            if (circle != null)
            {
                target.Circles.Add(new ColorCircle(circle.Center, (int)Math.Round(circle.Radius), context.ResolveColor(circle.Color))
                {
                    Radius = circle.Radius,
                    LineWidth = lineWidth
                });
                return true;
            }

            ColorArc arc = value as ColorArc;
            if (arc != null)
            {
                target.Arcs.Add(new ColorArc(arc.Center, arc.Radius, arc.StartAngle, arc.SweepAngle, context.ResolveColor(arc.Color))
                {
                    LineWidth = lineWidth
                });
                return true;
            }

            ColorEllipse ellipse = value as ColorEllipse;
            if (ellipse != null)
            {
                target.Ellipses.Add(new ColorEllipse(
                    ellipse.Center,
                    ellipse.Width,
                    ellipse.Height,
                    ellipse.Angle,
                    context.ResolveColor(ellipse.Color))
                {
                    LineWidth = lineWidth
                });
                return true;
            }

            ColorContour contour = value as ColorContour;
            if (contour != null)
            {
                target.Contours.Add(new ColorContour(
                    contour.Points == null ? new List<PointF>() : contour.Points.ToList(),
                    context.ResolveColor(contour.Color))
                {
                    LineWidth = lineWidth
                });
                return true;
            }

            MeasuredLine measuredLine = value as MeasuredLine;
            if (measuredLine != null && measuredLine.IsValid)
            {
                target.Lines.Add(new ColorLine(measuredLine.Start, measuredLine.End, context.ResolveColor(Color.Empty))
                {
                    LineWidth = lineWidth
                });
                return true;
            }

            if (value is PointF)
            {
                OverlayGeometryAdapterRegistry.AppendPointMarker(target, (PointF)value, context);
                return true;
            }

            if (value is Point)
            {
                Point point = (Point)value;
                OverlayGeometryAdapterRegistry.AppendPointMarker(target, new PointF(point.X, point.Y), context);
                return true;
            }

            if (value is Rectangle)
            {
                Rectangle rectangle = (Rectangle)value;
                target.Rects.Add(new ColorRotatedRect(new OpenCvSharp.Rect(
                    rectangle.X,
                    rectangle.Y,
                    rectangle.Width,
                    rectangle.Height))
                {
                    Color = context.ResolveColor(Color.Empty),
                    LineWidth = lineWidth
                });
                return true;
            }

            if (value is RectangleF)
            {
                RectangleF rectangle = (RectangleF)value;
                target.Rects.Add(new ColorRotatedRect(
                    rectangle.X + rectangle.Width / 2F,
                    rectangle.Y + rectangle.Height / 2F,
                    rectangle.Width,
                    rectangle.Height,
                    0F,
                    context.ResolveColor(Color.Empty))
                {
                    LineWidth = lineWidth
                });
                return true;
            }

            if (value is OpenCvSharp.Rect)
            {
                OpenCvSharp.Rect rectangle = (OpenCvSharp.Rect)value;
                target.Rects.Add(new ColorRotatedRect(rectangle)
                {
                    Color = context.ResolveColor(Color.Empty),
                    LineWidth = lineWidth
                });
                return true;
            }

            if (value is OpenCvSharp.RotatedRect)
            {
                OpenCvSharp.RotatedRect rectangle = (OpenCvSharp.RotatedRect)value;
                if (category == SubscriptionDataCategory.Ellipse)
                {
                    target.Ellipses.Add(new ColorEllipse(
                        new PointF(rectangle.Center.X, rectangle.Center.Y),
                        rectangle.Size.Width,
                        rectangle.Size.Height,
                        rectangle.Angle,
                        context.ResolveColor(Color.Empty))
                    {
                        LineWidth = lineWidth
                    });
                }
                else
                {
                    target.Rects.Add(new ColorRotatedRect(
                        rectangle.Center.X,
                        rectangle.Center.Y,
                        rectangle.Size.Width,
                        rectangle.Size.Height,
                        rectangle.Angle,
                        context.ResolveColor(Color.Empty))
                    {
                        LineWidth = lineWidth
                    });
                }
                return true;
            }

            if (value is OpenCvSharp.LineSegmentPoint)
            {
                OpenCvSharp.LineSegmentPoint segment = (OpenCvSharp.LineSegmentPoint)value;
                target.Lines.Add(new ColorLine(segment, context.ResolveColor(Color.Empty))
                {
                    LineWidth = lineWidth
                });
                return true;
            }

            if (value is OpenCvSharp.CircleSegment)
            {
                OpenCvSharp.CircleSegment circleSegment = (OpenCvSharp.CircleSegment)value;
                target.Circles.Add(new ColorCircle(circleSegment, context.ResolveColor(Color.Empty))
                {
                    LineWidth = lineWidth
                });
                return true;
            }

            addedCount = 0;
            return false;
        }
    }

    /// <summary>
    /// 按订阅类别处理点集合、轮廓集合和普通几何集合。
    /// </summary>
    internal sealed class EnumerableGeometryAdapter : IOverlayGeometryAdapter
    {
        /// <inheritdoc />
        public bool CanHandle(object value, SubscriptionDataCategory category)
        {
            return value is IEnumerable && !(value is string) && !(value is byte[]);
        }

        /// <inheritdoc />
        public bool TryAppend(
            AlgorithmResult target,
            object value,
            INodeResult sourceResult,
            SubscriptionDataCategory category,
            OverlayGeometryRenderContext context,
            out int addedCount)
        {
            addedCount = 0;
            IEnumerable enumerable = value as IEnumerable;
            if (enumerable == null)
                return false;

            if (category == SubscriptionDataCategory.Contour || category == SubscriptionDataCategory.Region)
            {
                List<PointF> points = ReadPointList(enumerable);
                if (points.Count > 0)
                {
                    if (category == SubscriptionDataCategory.Region &&
                        points.Count > 2 &&
                        points[0] != points[points.Count - 1])
                    {
                        points.Add(points[0]);
                    }

                    target.Contours.Add(new ColorContour(points, context.ResolveColor(Color.Empty))
                    {
                        LineWidth = context.ResolveLineWidth()
                    });
                    addedCount = 1;
                    return true;
                }
            }

            bool recognized = false;
            foreach (object item in enumerable)
            {
                if (item == null)
                    continue;

                SubscriptionDataCategory itemCategory = ResolveItemCategory(category, item);
                int itemCount;
                if (OverlayGeometryAdapterRegistry.TryAppend(
                    target,
                    item,
                    sourceResult,
                    itemCategory,
                    context,
                    out itemCount))
                {
                    recognized = true;
                    addedCount += itemCount;
                }
            }

            return recognized;
        }

        /// <summary>
        /// 把扁平可枚举值读取成二维点集合。
        /// </summary>
        /// <param name="enumerable">待读取集合。</param>
        /// <returns>读取到的二维点。</returns>
        private static List<PointF> ReadPointList(IEnumerable enumerable)
        {
            List<PointF> points = new List<PointF>();
            foreach (object item in enumerable)
            {
                if (item is PointF)
                    points.Add((PointF)item);
                else if (item is Point)
                {
                    Point point = (Point)item;
                    points.Add(new PointF(point.X, point.Y));
                }
                else
                    return new List<PointF>();
            }
            return points;
        }

        /// <summary>
        /// 根据父集合语义和元素实际类型确定递归分派类别。
        /// </summary>
        /// <param name="parentCategory">父集合类别。</param>
        /// <param name="item">集合元素。</param>
        /// <returns>元素使用的数据类别。</returns>
        private static SubscriptionDataCategory ResolveItemCategory(
            SubscriptionDataCategory parentCategory,
            object item)
        {
            if (parentCategory == SubscriptionDataCategory.PointCollection)
                return SubscriptionDataCategory.Point;
            if (parentCategory == SubscriptionDataCategory.Contour ||
                parentCategory == SubscriptionDataCategory.Region)
            {
                return parentCategory;
            }
            if (item is AlgorithmResult)
                return SubscriptionDataCategory.AlgorithmResult;
            return SubscriptionTypeCompatibility.ResolveCategory(item.GetType());
        }
    }
}
