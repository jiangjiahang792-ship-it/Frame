using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OpenCvSharp;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw
{
    /// <summary>输入图像中的单个截取矩形，与显示项及上游检测区域相互独立。</summary>
    public sealed class OverlayDisplayCropRegion
    {
        /// <summary>中心行。</summary>
        public float Row { get; set; }
        /// <summary>中心列。</summary>
        public float Column { get; set; }
        /// <summary>角度，单位弧度。</summary>
        public float Phi { get; set; }
        /// <summary>半宽，单位像素。</summary>
        public float Length1 { get; set; }
        /// <summary>半高，单位像素。</summary>
        public float Length2 { get; set; }
        /// <summary>复制编辑参数，避免拖动修改已保存方案。</summary>
        public OverlayDisplayCropRegion Clone() => (OverlayDisplayCropRegion)MemberwiseClone();
    }

    internal static partial class ResultOverlayDrawBuilder
    {
        /// <summary>生成截取输出，图像与所有图像坐标标注共用同一仿射矩阵，不修改上游数据。</summary>
        internal static OutputImage BuildCroppedOutput(Mat source, AlgorithmResult display, List<Rect> rectangles, NodeParamResultOverlayDraw param)
        {
            var roi = param.DisplayCropRegion;
            if (source == null || source.Empty() || roi == null) throw new ArgumentException("请先绘制并确认截取ROI。");
            double[] values = { roi.Column, roi.Row, roi.Phi, roi.Length1, roi.Length2 };
            if (values.Any(v => double.IsNaN(v) || double.IsInfinity(v)) || roi.Length1 < 0.5 || roi.Length2 < 0.5 || roi.Length1 > 16384 || roi.Length2 > 16384)
                throw new ArgumentException("截取ROI的坐标或尺寸无效。");
            int width = (int)Math.Round(roi.Length1 * 2), height = (int)Math.Round(roi.Length2 * 2);
            if ((long)width * height > 100000000) throw new ArgumentException("截取ROI尺寸过大。");
            var bounds = new OpenCvSharp.RotatedRect(new Point2f(roi.Column, roi.Row), new Size2f(width, height),
                (float)(roi.Phi * 180 / Math.PI)).BoundingRect();
            if (bounds.Right <= 0 || bounds.Bottom <= 0 || bounds.Left >= source.Width || bounds.Top >= source.Height)
                throw new ArgumentException("截取ROI与输入图像没有交集。");
            double cos = Math.Cos(roi.Phi), sin = Math.Sin(roi.Phi);
            int rotation = NormalizeRotationAngle(param.RotationAngle);
            int outputWidth = rotation == 90 || rotation == 270 ? height : width;
            int outputHeight = rotation == 90 || rotation == 270 ? width : height;
            // 以像素中心为坐标，W-1/H-1 保证旋转图像与标注没有一像素偏差。
            double a = cos, b = sin, c = -sin, d = cos;
            double tx = (width - 1) / 2d - a * roi.Column - b * roi.Row;
            double ty = (height - 1) / 2d - c * roi.Column - d * roi.Row;
            double aa = a, bb = b, cc = c, dd = d, xx = tx, yy = ty;
            switch (rotation)
            {
                case 90: a = -cc; b = -dd; tx = height - 1 - yy; c = aa; d = bb; ty = xx; break;
                case 180: a = -aa; b = -bb; tx = width - 1 - xx; c = -cc; d = -dd; ty = height - 1 - yy; break;
                case 270: a = cc; b = dd; tx = yy; c = -aa; d = -bb; ty = width - 1 - xx; break;
            }
            Func<PointF, PointF> transform = point => new PointF((float)(a * point.X + b * point.Y + tx), (float)(c * point.X + d * point.Y + ty));
            var cropped = new Mat();
            OutputImage output = null;
            try
            {
                using (var matrix = new Mat(2, 3, MatType.CV_64FC1))
                {
                    matrix.Set(0, 0, a); matrix.Set(0, 1, b); matrix.Set(0, 2, tx);
                    matrix.Set(1, 0, c); matrix.Set(1, 1, d); matrix.Set(1, 2, ty);
                    Cv2.WarpAffine(source, cropped, matrix, new OpenCvSharp.Size(outputWidth, outputHeight), InterpolationFlags.Linear, BorderTypes.Constant, Scalar.All(0));
                }
                // 复用现有完整克隆逻辑，角度零时不改变任何坐标或文字属性。
                var result = RotateDisplayResult(display, source.Width, source.Height, 0);
                float angle = rotation - (float)(roi.Phi * 180 / Math.PI);
                TransformCropGeometry(result, transform, angle);
                result.ClipToImageBounds = true;
                output = OutputImage.FromOwnedSingleImage(cropped);
                cropped = null;
                output.DisplayResult = result;
                output.Rectangles = new List<Rect>();
                foreach (var rect in rectangles ?? new List<Rect>())
                {
                    var points = new[] { new PointF(rect.Left, rect.Top), new PointF(rect.Right, rect.Top), new PointF(rect.Right, rect.Bottom), new PointF(rect.Left, rect.Bottom) }.Select(transform).ToArray();
                    int left = Math.Max(0, (int)Math.Floor(points.Min(p => p.X))), top = Math.Max(0, (int)Math.Floor(points.Min(p => p.Y)));
                    int right = Math.Min(outputWidth, (int)Math.Ceiling(points.Max(p => p.X))), bottom = Math.Min(outputHeight, (int)Math.Ceiling(points.Max(p => p.Y)));
                    if (right > left && bottom > top) output.Rectangles.Add(new Rect(left, top, right - left, bottom - top));
                }
                return output;
            }
            catch { output?.Dispose(); throw; }
            finally { cropped?.Dispose(); }
        }

        /// <summary>统一转换所有几何；绝对位置文字移动锚点，四角文字保留布局语义和可读方向。</summary>
        private static void TransformCropGeometry(AlgorithmResult result, Func<PointF, PointF> transform, float angle)
        {
            foreach (var rect in result.Rects.Concat(result.RectsNgMap.Values.SelectMany(v => v)))
            {
                var old = rect.RotatedRect;
                var center = transform(new PointF(old.center.x, old.center.y));
                rect.RotatedRect = new ColorRotatedRect(center.X, center.Y, old.size.width, old.size.height, old.angle + angle, rect.Color).RotatedRect;
            }
            foreach (var text in result.Texts)
                if (text.UseImagePosition) text.ImagePosition = transform(text.ImagePosition);
            foreach (var line in result.Lines) { line.P1 = transform(line.P1); line.P2 = transform(line.P2); }
            foreach (var circle in result.Circles) circle.Center = transform(circle.Center);
            foreach (var arc in result.Arcs) { arc.Center = transform(arc.Center); arc.StartAngle += angle; }
            foreach (var ellipse in result.Ellipses) { ellipse.Center = transform(ellipse.Center); ellipse.Angle += angle; }
            foreach (var contour in result.Contours) contour.Points = contour.Points.Select(transform).ToList();
        }
    }
}
