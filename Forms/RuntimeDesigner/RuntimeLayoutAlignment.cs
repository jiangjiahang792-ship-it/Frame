using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>仅描述编辑期的参考线，不写入方案和模板。</summary>
    public sealed class RuntimeAlignmentGuide
    {
        /// <summary>是否为纵向参考线。</summary>
        public bool Vertical { get; set; }
        /// <summary>参考线在设计坐标中的位置。</summary>
        public float Position { get; set; }
        /// <summary>参考线起点。</summary>
        public float Start { get; set; }
        /// <summary>参考线终点。</summary>
        public float End { get; set; }
    }

    /// <summary>一次移动、拖入或缩放的计算结果。</summary>
    public sealed class RuntimeAlignmentResult
    {
        /// <summary>经过网格、智能吸附及页面边界约束的矩形。</summary>
        public Rectangle Bounds { get; set; }
        /// <summary>与最终矩形一致的辅助线。</summary>
        public List<RuntimeAlignmentGuide> Guides { get; } = new List<RuntimeAlignmentGuide>();
    }

    /// <summary>无 UI 状态的几何服务；网格与吸附共用坐标，阈值保持屏幕像素大小。</summary>
    public static class RuntimeLayoutAlignment
    {
        /// <summary>可见细网格与实际吸附的共同间距。</summary>
        public const int GridSpacing = 10;
        /// <summary>最小组件宽度。</summary>
        private const int MinimumWidth = 48;
        /// <summary>最小组件高度。</summary>
        private const int MinimumHeight = 32;

        /// <summary>计算最终位置；handle 为 -1 时移动，否则为顺时针八方向手柄。</summary>
        public static RuntimeAlignmentResult Resolve(RuntimeLayoutDocument document, string excludedId,
            Rectangle proposed, Rectangle original, int handle, float zoom, bool snap)
        {
            return ResolveGroup(document, excludedId == null ? new string[0] : new[] { excludedId }, proposed, original, handle, zoom, snap);
        }

        /// <summary>整组移动时排除所有选中原件，复制时保留原件作为对齐目标。</summary>
        public static RuntimeAlignmentResult ResolveGroup(RuntimeLayoutDocument document, IEnumerable<string> excludedIds,
            Rectangle proposed, Rectangle original, int handle, float zoom, bool snap)
        {
            bool moving = handle < 0;
            bool left = handle == 0 || handle == 6 || handle == 7;
            bool right = handle == 2 || handle == 3 || handle == 4;
            bool top = handle == 0 || handle == 1 || handle == 2;
            bool bottom = handle == 4 || handle == 5 || handle == 6;
            var bounds = proposed;
            var excluded = new HashSet<string>(excludedIds ?? Enumerable.Empty<string>());
            var targets = document.Elements.Where(e => !excluded.Contains(e.Id)).Select(e => e.Bounds).ToList();
            targets.Add(new Rectangle(0, 0, document.Width, document.Height));
            float tolerance = 6 / Math.Max(0.05f, zoom);
            if (snap)
            {
                int x = moving ? proposed.Left : left ? proposed.Left : proposed.Right;
                int y = moving ? proposed.Top : top ? proposed.Top : proposed.Bottom;
                int alignedX = SnapGrid(x), alignedY = SnapGrid(y);
                float? dx = FindAdjustment(proposed, targets, true, moving, left, tolerance);
                float? dy = FindAdjustment(proposed, targets, false, moving, top, tolerance);
                if (dx.HasValue) alignedX = (int)Math.Round(x + dx.Value);
                if (dy.HasValue) alignedY = (int)Math.Round(y + dy.Value);
                if (moving) bounds.Location = new Point(alignedX, alignedY);
                else bounds = Rectangle.FromLTRB(left ? alignedX : original.Left, top ? alignedY : original.Top,
                    right ? alignedX : original.Right, bottom ? alignedY : original.Bottom);
            }
            if (moving)
            {
                bounds.Width = Math.Min(document.Width, bounds.Width);
                bounds.Height = Math.Min(document.Height, bounds.Height);
                bounds.X = Math.Max(0, Math.Min(document.Width - bounds.Width, bounds.X));
                bounds.Y = Math.Max(0, Math.Min(document.Height - bounds.Height, bounds.Y));
            }
            else
            {
                // 只约束正在拖动的边，左上手柄不能意外移动固定的右下边。
                int l = left ? Math.Max(0, Math.Min(original.Right - MinimumWidth, bounds.Left)) : original.Left;
                int t = top ? Math.Max(0, Math.Min(original.Bottom - MinimumHeight, bounds.Top)) : original.Top;
                int r = right ? Math.Min(document.Width, Math.Max(original.Left + MinimumWidth, bounds.Right)) : original.Right;
                int b = bottom ? Math.Min(document.Height, Math.Max(original.Top + MinimumHeight, bounds.Bottom)) : original.Bottom;
                bounds = Rectangle.FromLTRB(l, t, r, b);
            }
            var result = new RuntimeAlignmentResult { Bounds = bounds };
            foreach (Rectangle target in targets)
            {
                if (moving || left || right) AddGuides(result, target, true, moving, left);
                if (moving || top || bottom) AddGuides(result, target, false, moving, top);
            }
            return result;
        }

        /// <summary>将绝对坐标吸附到细网格。</summary>
        private static int SnapGrid(int value) => (int)Math.Round(value / (double)GridSpacing, MidpointRounding.AwayFromZero) * GridSpacing;

        /// <summary>列出轴向边缘和中心；缩放时只使用活动边。</summary>
        private static float[] Anchors(Rectangle rect, bool vertical, bool moving, bool leading)
        {
            float start = vertical ? rect.Left : rect.Top;
            float end = vertical ? rect.Right : rect.Bottom;
            return moving ? new[] { start, (start + end) / 2, end } : new[] { leading ? start : end };
        }

        /// <summary>只采用屏幕阈值内最近的边或中心，不按缩放比例扩大视觉吸附范围。</summary>
        private static float? FindAdjustment(Rectangle bounds, List<Rectangle> targets, bool vertical, bool moving, bool leading, float tolerance)
        {
            float? best = null;
            foreach (float source in Anchors(bounds, vertical, moving, leading))
                foreach (Rectangle target in targets)
                    foreach (float destination in Anchors(target, vertical, true, false))
                    {
                        float distance = destination - source;
                        if (Math.Abs(distance) <= tolerance && (!best.HasValue || Math.Abs(distance) < Math.Abs(best.Value))) best = distance;
                    }
            return best;
        }

        /// <summary>依据最终几何生成精确辅助线，并合并同一坐标的重复线。</summary>
        private static void AddGuides(RuntimeAlignmentResult result, Rectangle target, bool vertical, bool moving, bool leading)
        {
            foreach (float source in Anchors(result.Bounds, vertical, moving, leading))
                foreach (float destination in Anchors(target, vertical, true, false))
                {
                    if (Math.Abs(source - destination) > 0.51f) continue;
                    float start = vertical ? Math.Min(target.Top, result.Bounds.Top) : Math.Min(target.Left, result.Bounds.Left);
                    float end = vertical ? Math.Max(target.Bottom, result.Bounds.Bottom) : Math.Max(target.Right, result.Bounds.Right);
                    var existing = result.Guides.FirstOrDefault(g => g.Vertical == vertical && Math.Abs(g.Position - destination) < 0.1f);
                    if (existing == null) result.Guides.Add(new RuntimeAlignmentGuide { Vertical = vertical, Position = destination, Start = start, End = end });
                    else { existing.Start = Math.Min(existing.Start, start); existing.End = Math.Max(existing.End, end); }
                }
        }
    }
}
