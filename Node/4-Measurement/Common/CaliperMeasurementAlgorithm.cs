using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace TDJS_Vision.Node._4_Measurement.Common
{
    public enum CaliperEdgePolarity
    {
        DarkToLight,
        LightToDark,
        Both
    }

    public enum CaliperEdgeFindMode
    {
        Best,
        First,
        Last
    }

    public class CaliperLineParams
    {
        public float StartX { get; set; }
        public float StartY { get; set; }
        public float EndX { get; set; }
        public float EndY { get; set; }
        public float CaliperWidth { get; set; } = 20;
        public float CaliperHeight { get; set; } = 200;
        public int Count { get; set; } = 15;
        public int EdgeStrength { get; set; } = 20;
        public CaliperEdgePolarity Polarity { get; set; } = CaliperEdgePolarity.Both;
        public CaliperEdgeFindMode FindMode { get; set; } = CaliperEdgeFindMode.Best;
        public int Direction { get; set; }
        public int BlurSize { get; set; } = 3;
    }

    public class CaliperLineMeasureResult
    {
        public List<PointF> EdgePoints { get; set; } = new List<PointF>();
        public PointF LineStart { get; set; }
        public PointF LineEnd { get; set; }
        public bool Success { get; set; }
        public double AlgorithmMs { get; set; }
        public int PointCount => EdgePoints.Count;
    }

    public class CaliperCircleParams
    {
        public float CenterX { get; set; }
        public float CenterY { get; set; }
        public float Radius { get; set; }
        public float StartAngle { get; set; }
        public float EndAngle { get; set; } = 360;
        public float CaliperWidth { get; set; } = 10;
        public float CaliperHeight { get; set; } = 60;
        public int Count { get; set; } = 30;
        /// <summary>
        /// 是否启用拟合有效点数限制。
        /// </summary>
        public bool EnableFitValidPointCount { get; set; }
        /// <summary>
        /// 拟合有效点数阈值，边缘点数量必须大于该值才执行圆拟合。
        /// </summary>
        public int FitValidPointCount { get; set; } = 10;
        public int EdgeStrength { get; set; } = 20;
        public CaliperEdgePolarity Polarity { get; set; } = CaliperEdgePolarity.Both;
        public CaliperEdgeFindMode FindMode { get; set; } = CaliperEdgeFindMode.Best;
        public int Direction { get; set; }
        public int BlurSize { get; set; } = 3;
    }

    public class CaliperCircleMeasureResult
    {
        public List<PointF> EdgePoints { get; set; } = new List<PointF>();
        public PointF Center { get; set; }
        public float Radius { get; set; }
        public bool Success { get; set; }
        public double AlgorithmMs { get; set; }
        /// <summary>
        /// 圆拟合失败时返回给界面与显示结果的原因。
        /// </summary>
        public string ErrorMessage { get; set; }
        public int PointCount => EdgePoints.Count;
    }

    public class CaliperEllipseParams
    {
        public float CenterX { get; set; }
        public float CenterY { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public float Angle { get; set; }
        public int Count { get; set; } = 30;
        /// <summary>
        /// 是否启用拟合有效点数限制。
        /// </summary>
        public bool EnableFitValidPointCount { get; set; }
        /// <summary>
        /// 拟合有效点数阈值，边缘点数量必须大于该值才执行椭圆拟合。
        /// </summary>
        public int FitValidPointCount { get; set; } = 10;
        public float CaliperWidth { get; set; } = 10;
        public float CaliperHeight { get; set; } = 60;
        public int EdgeStrength { get; set; } = 20;
        public CaliperEdgePolarity Polarity { get; set; } = CaliperEdgePolarity.Both;
        public CaliperEdgeFindMode FindMode { get; set; } = CaliperEdgeFindMode.Best;
        public int Direction { get; set; }
        public int BlurSize { get; set; } = 3;
    }

    public class CaliperEllipseMeasureResult
    {
        public bool Success { get; set; }
        public Point2f Center { get; set; }
        public Size2f Size { get; set; }
        public float Angle { get; set; }
        public List<PointF> EdgePoints { get; set; } = new List<PointF>();
        public List<PointF> FailedPoints { get; set; } = new List<PointF>();
        public double AlgorithmMs { get; set; }
        public string ErrorMessage { get; set; }
        public int PointCount => EdgePoints.Count;
    }

    public static class CaliperMeasurementAlgorithm
    {
        public static Mat ToGray(Mat source)
        {
            if (source == null || source.Empty())
                throw new ArgumentException("输入图像为空！");

            Mat gray = new Mat();
            if (source.Channels() == 1)
                source.CopyTo(gray);
            else if (source.Channels() == 4)
                Cv2.CvtColor(source, gray, ColorConversionCodes.BGRA2GRAY);
            else
                Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
            return gray;
        }

        public static CaliperLineMeasureResult FindLine(Mat gray, CaliperLineParams p)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = new CaliperLineMeasureResult();
            ValidateGray(gray);

            float axisX = p.EndX - p.StartX;
            float axisY = p.EndY - p.StartY;
            float axisLen = (float)Math.Sqrt(axisX * axisX + axisY * axisY);
            if (axisLen < 1)
                return Finish(result, sw);

            float uAx = axisX / axisLen;
            float uAy = axisY / axisLen;
            float uPerpX = -uAy;
            float uPerpY = uAx;
            float scanDirX = p.Direction == 0 ? uPerpX : uAx;
            float scanDirY = p.Direction == 0 ? uPerpY : uAy;
            float scanRange = p.Direction == 0 ? p.CaliperHeight : p.CaliperWidth;

            int count = Math.Max(1, p.Count);
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0.5f : (float)i / (count - 1);
                float cx = p.StartX + axisX * t;
                float cy = p.StartY + axisY * t;
                PointF? edgePoint = FindEdgeOnProfile(gray, cx, cy, scanDirX, scanDirY, scanRange, p.BlurSize, p.EdgeStrength, p.Polarity, p.FindMode);
                if (edgePoint.HasValue)
                    result.EdgePoints.Add(edgePoint.Value);
            }

            if (result.EdgePoints.Count >= 2)
            {
                FitLine(result);
                result.Success = true;
            }

            return Finish(result, sw);
        }

        public static CaliperCircleMeasureResult FindCircle(Mat gray, CaliperCircleParams p)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = new CaliperCircleMeasureResult();
            ValidateGray(gray);

            float spanAngle = p.EndAngle - p.StartAngle;
            if (spanAngle < 0 && p.EndAngle < p.StartAngle + 360)
                spanAngle += 360;

            int count = Math.Max(1, p.Count);
            for (int i = 0; i < count; i++)
            {
                float ratio = count == 1 ? 0.5f : (float)i / (count - 1);
                if (Math.Abs(Math.Abs(spanAngle) - 360) < 0.1f && count > 1)
                    ratio = (float)i / count;

                float angleDeg = p.StartAngle + spanAngle * ratio;
                float angleRad = (float)(angleDeg * Math.PI / 180.0);
                float cx = p.CenterX + p.Radius * (float)Math.Cos(angleRad);
                float cy = p.CenterY + p.Radius * (float)Math.Sin(angleRad);
                float scanDirX = (float)Math.Cos(angleRad);
                float scanDirY = (float)Math.Sin(angleRad);
                if (p.Direction == 1)
                {
                    scanDirX = -scanDirX;
                    scanDirY = -scanDirY;
                }

                PointF? edgePoint = FindEdgeOnProfile(gray, cx, cy, scanDirX, scanDirY, p.CaliperHeight, p.BlurSize, p.EdgeStrength, p.Polarity, p.FindMode);
                if (edgePoint.HasValue)
                    result.EdgePoints.Add(edgePoint.Value);
            }

            int fitValidPointCount = Math.Max(0, p.FitValidPointCount);
            if (p.EnableFitValidPointCount && result.EdgePoints.Count <= fitValidPointCount)
            {
                result.ErrorMessage = $"有效点数 {result.EdgePoints.Count} 未超过拟合有效点数 {fitValidPointCount}，不执行拟合";
                return Finish(result, sw);
            }

            if (result.EdgePoints.Count >= 3)
            {
                FitCircle(result);
                result.Success = result.Radius > 0;
            }
            else
            {
                result.ErrorMessage = $"只有 {result.EdgePoints.Count} 个点，拟合需要至少 3 个点";
            }

            return Finish(result, sw);
        }

        public static CaliperEllipseMeasureResult FindEllipse(Mat gray, CaliperEllipseParams p)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = new CaliperEllipseMeasureResult();
            ValidateGray(gray);

            float a = p.Width / 2f;
            float b = p.Height / 2f;
            if (a <= 0 || b <= 0)
            {
                result.ErrorMessage = "无效椭圆尺寸";
                return Finish(result, sw);
            }

            float phi = (float)(p.Angle * Math.PI / 180.0);
            float cosPhi = (float)Math.Cos(phi);
            float sinPhi = (float)Math.Sin(phi);
            int count = Math.Max(1, p.Count);

            for (int i = 0; i < count; i++)
            {
                float t = (float)(2.0 * Math.PI * i / count);
                float cost = (float)Math.Cos(t);
                float sint = (float)Math.Sin(t);
                float lx = a * cost;
                float ly = b * sint;
                float nx = cost / a;
                float ny = sint / b;
                float cx = lx * cosPhi - ly * sinPhi + p.CenterX;
                float cy = lx * sinPhi + ly * cosPhi + p.CenterY;
                float rnx = nx * cosPhi - ny * sinPhi;
                float rny = nx * sinPhi + ny * cosPhi;
                float len = (float)Math.Sqrt(rnx * rnx + rny * rny);
                if (len < 0.0001f)
                    continue;

                float scanDirX = rnx / len;
                float scanDirY = rny / len;
                if (p.Direction == 1)
                {
                    scanDirX = -scanDirX;
                    scanDirY = -scanDirY;
                }

                PointF? edgePoint = FindEdgeOnProfile(gray, cx, cy, scanDirX, scanDirY, p.CaliperHeight, p.BlurSize, p.EdgeStrength, p.Polarity, p.FindMode);
                if (edgePoint.HasValue)
                    result.EdgePoints.Add(edgePoint.Value);
                else
                    result.FailedPoints.Add(new PointF(cx, cy));
            }

            int fitValidPointCount = Math.Max(0, p.FitValidPointCount);
            if (p.EnableFitValidPointCount && result.EdgePoints.Count <= fitValidPointCount)
            {
                result.ErrorMessage = $"有效点数 {result.EdgePoints.Count} 未超过拟合有效点数 {fitValidPointCount}，不执行拟合";
                return Finish(result, sw);
            }

            if (result.EdgePoints.Count >= 5)
                FitEllipse(result);
            else
                result.ErrorMessage = $"只有 {result.EdgePoints.Count} 个点，拟合需要至少 5 个点";

            return Finish(result, sw);
        }

        private static void ValidateGray(Mat gray)
        {
            if (gray == null || gray.Empty())
                throw new ArgumentException("灰度图像为空！");
            if (gray.Channels() != 1)
                throw new ArgumentException("卡尺算法需要单通道灰度图像！");
        }

        private static PointF? FindEdgeOnProfile(
            Mat gray,
            float cx,
            float cy,
            float scanDirX,
            float scanDirY,
            float scanRange,
            int blurSize,
            int edgeStrength,
            CaliperEdgePolarity polarity,
            CaliperEdgeFindMode findMode)
        {
            int halfRange = Math.Max(1, (int)(scanRange / 2));
            int profileLen = halfRange * 2 + 1;
            float[] profile = new float[profileLen];
            float[] posX = new float[profileLen];
            float[] posY = new float[profileLen];

            for (int j = 0; j < profileLen; j++)
            {
                float offset = j - halfRange;
                float px = cx + scanDirX * offset;
                float py = cy + scanDirY * offset;
                posX[j] = px;
                posY[j] = py;

                int ix = (int)(px + 0.5f);
                int iy = (int)(py + 0.5f);
                profile[j] = IsInside(gray, ix, iy) ? gray.At<byte>(iy, ix) : 0;
            }

            if (blurSize > 1)
                profile = SmoothProfile(profile, blurSize);

            float[] gradient = new float[profileLen];
            for (int j = 1; j < profileLen - 1; j++)
                gradient[j] = (profile[j + 1] - profile[j - 1]) / 2f;

            return FindEdgeInProfile(gradient, posX, posY, edgeStrength, polarity, findMode);
        }

        private static bool IsInside(Mat gray, int x, int y)
        {
            return (uint)x < (uint)gray.Cols && (uint)y < (uint)gray.Rows;
        }

        private static float[] SmoothProfile(float[] profile, int blurSize)
        {
            int halfK = Math.Min(blurSize / 2, 3);
            float[] smoothed = new float[profile.Length];
            for (int i = 0; i < profile.Length; i++)
            {
                float sum = 0;
                int count = 0;
                for (int k = -halfK; k <= halfK; k++)
                {
                    int idx = i + k;
                    if (idx >= 0 && idx < profile.Length)
                    {
                        sum += profile[idx];
                        count++;
                    }
                }

                smoothed[i] = count == 0 ? profile[i] : sum / count;
            }

            return smoothed;
        }

        private static PointF? FindEdgeInProfile(
            float[] gradient,
            float[] px,
            float[] py,
            int threshold,
            CaliperEdgePolarity polarity,
            CaliperEdgeFindMode mode)
        {
            var candidates = new List<EdgeCandidate>();
            for (int i = 1; i < gradient.Length - 1; i++)
            {
                float g = gradient[i];
                float absG = Math.Abs(g);
                if (absG < threshold)
                    continue;
                if (polarity == CaliperEdgePolarity.DarkToLight && g <= 0)
                    continue;
                if (polarity == CaliperEdgePolarity.LightToDark && g >= 0)
                    continue;

                candidates.Add(new EdgeCandidate(i, absG));
            }

            if (candidates.Count == 0)
                return null;

            int bestIdx;
            switch (mode)
            {
                case CaliperEdgeFindMode.First:
                    bestIdx = candidates[0].Index;
                    break;
                case CaliperEdgeFindMode.Last:
                    bestIdx = candidates[candidates.Count - 1].Index;
                    break;
                case CaliperEdgeFindMode.Best:
                default:
                    bestIdx = candidates.OrderByDescending(c => c.AbsGradient).First().Index;
                    break;
            }

            if (bestIdx > 0 && bestIdx < gradient.Length - 1)
            {
                float gPrev = Math.Abs(gradient[bestIdx - 1]);
                float gCurr = Math.Abs(gradient[bestIdx]);
                float gNext = Math.Abs(gradient[bestIdx + 1]);
                float denom = gPrev - 2 * gCurr + gNext;
                if (Math.Abs(denom) > 0.001f)
                {
                    float subOffset = 0.5f * (gPrev - gNext) / denom;
                    subOffset = Math.Max(-0.5f, Math.Min(0.5f, subOffset));
                    float fIdx = bestIdx + subOffset;
                    int lo = (int)Math.Floor(fIdx);
                    int hi = Math.Min(lo + 1, gradient.Length - 1);
                    float frac = fIdx - lo;
                    return new PointF(
                        px[lo] * (1 - frac) + px[hi] * frac,
                        py[lo] * (1 - frac) + py[hi] * frac);
                }
            }

            return new PointF(px[bestIdx], py[bestIdx]);
        }

        private static void FitLine(CaliperLineMeasureResult result)
        {
            Point2f[] cvPts = result.EdgePoints.Select(p => new Point2f(p.X, p.Y)).ToArray();
            using (InputArray pts = InputArray.Create(cvPts))
            using (Mat line = new Mat())
            {
                Cv2.FitLine(pts, line, DistanceTypes.L2, 0, 0.01, 0.01);
                float vx = line.At<float>(0, 0);
                float vy = line.At<float>(1, 0);
                float x0 = line.At<float>(2, 0);
                float y0 = line.At<float>(3, 0);
                float minT = float.MaxValue;
                float maxT = float.MinValue;

                foreach (PointF point in result.EdgePoints)
                {
                    float t = (point.X - x0) * vx + (point.Y - y0) * vy;
                    minT = Math.Min(minT, t);
                    maxT = Math.Max(maxT, t);
                }

                result.LineStart = new PointF(x0 + vx * minT, y0 + vy * minT);
                result.LineEnd = new PointF(x0 + vx * maxT, y0 + vy * maxT);
            }
        }

        private static void FitCircle(CaliperCircleMeasureResult result)
        {
            int n = result.EdgePoints.Count;
            using (Mat a = new Mat(n, 3, MatType.CV_32F))
            using (Mat b = new Mat(n, 1, MatType.CV_32F))
            using (Mat x = new Mat())
            {
                for (int i = 0; i < n; i++)
                {
                    float px = result.EdgePoints[i].X;
                    float py = result.EdgePoints[i].Y;
                    a.Set(i, 0, px);
                    a.Set(i, 1, py);
                    a.Set(i, 2, 1.0f);
                    b.Set(i, 0, px * px + py * py);
                }

                if (!Cv2.Solve(a, b, x, DecompTypes.SVD))
                    return;

                float aa = x.At<float>(0, 0);
                float bb = x.At<float>(1, 0);
                float cc = x.At<float>(2, 0);
                float cx = aa / 2.0f;
                float cy = bb / 2.0f;
                float radius = (float)Math.Sqrt(Math.Max(0, cc + (aa * aa + bb * bb) / 4.0f));
                result.Center = new PointF(cx, cy);
                result.Radius = radius;
            }
        }

        private static void FitEllipse(CaliperEllipseMeasureResult result)
        {
            Point2f[] cvPts = result.EdgePoints.Select(p => new Point2f(p.X, p.Y)).ToArray();
            OpenCvSharp.RotatedRect fit = Cv2.FitEllipse(cvPts);
            result.Success = true;
            result.Center = fit.Center;
            result.Size = fit.Size;
            result.Angle = fit.Angle;
        }

        private static T Finish<T>(T result, System.Diagnostics.Stopwatch sw)
        {
            sw.Stop();
            dynamic dynamicResult = result;
            dynamicResult.AlgorithmMs = sw.Elapsed.TotalMilliseconds;
            return result;
        }

        private struct EdgeCandidate
        {
            public EdgeCandidate(int index, float absGradient)
            {
                Index = index;
                AbsGradient = absGradient;
            }

            public int Index { get; }
            public float AbsGradient { get; }
        }
    }
}
