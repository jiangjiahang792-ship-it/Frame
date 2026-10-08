using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using OpenCvSharp;

namespace TDJS_Vision.Node._3_Detection.TDAI.Yolo8
{
    /// <summary>可跨隔离进程传输的原图掩膜几何，不持有原生图像资源。</summary>
    public sealed class SegMaskGeometry
    {
        /// <summary>前景像素的原图包围范围。</summary>
        public Rect Bounds { get; set; }
        /// <summary>中央60%宽度逐列首个前景像素的最大Y坐标。</summary>
        public int UpperEdgeLowestY { get; set; }
        /// <summary>原图坐标下的所有外部轮廓。</summary>
        public PointF[][] Contours { get; set; }

        /// <summary>一次扫描掩膜提取边界与顶部最低点；与FAKRA SEG测试的取点规则一致。</summary>
        public static SegMaskGeometry FromMask(Mat mask, int offsetX = 0, int offsetY = 0)
        {
            if (mask == null || mask.Empty() || mask.Type() != MatType.CV_8UC1)
                return null;
            int left = mask.Width, right = -1, top = mask.Height, bottom = -1;
            var columnTops = Enumerable.Repeat(-1, mask.Width).ToArray();
            var row = new byte[mask.Width];
            for (int y = 0; y < mask.Height; y++)
            {
                Marshal.Copy(mask.Ptr(y), row, 0, row.Length);
                for (int x = 0; x < row.Length; x++)
                {
                    if (row[x] == 0) continue;
                    left = Math.Min(left, x); right = Math.Max(right, x);
                    top = Math.Min(top, y); bottom = y;
                    if (columnTops[x] < 0) columnTops[x] = y;
                }
            }
            if (right < left) return null;
            int margin = (int)Math.Floor((right - left + 1) * 0.2 + 1e-9);
            int lowest = -1;
            for (int x = left + margin; x <= right - margin; x++)
                lowest = Math.Max(lowest, columnTops[x]);
            OpenCvSharp.Point[][] contours;
            HierarchyIndex[] hierarchy;
            Cv2.FindContours(mask, out contours, out hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
            return new SegMaskGeometry
            {
                Bounds = new Rect(left + offsetX, top + offsetY, right - left + 1, bottom - top + 1),
                UpperEdgeLowestY = lowest < 0 ? -1 : lowest + offsetY,
                Contours = contours.Select(c => c.Select(p => new PointF(p.X + offsetX, p.Y + offsetY)).ToArray()).ToArray()
            };
        }
    }
}
