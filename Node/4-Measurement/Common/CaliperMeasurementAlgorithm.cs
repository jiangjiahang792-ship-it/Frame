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

    /// <summary>
    /// 指定卡尺灰度剖面的采样方式。
    /// </summary>
    public enum CaliperSamplingMode
    {
        /// <summary>
        /// 读取扫描宽度上的三个双线性样本，兼顾运行速度和旋转采样稳定性。
        /// </summary>
        Fast = 0,

        /// <summary>
        /// 沿卡尺宽度进行多点双线性采样并求平均，优先保证抗干扰能力。
        /// </summary>
        AntiInterference = 1
    }

    /// <summary>定义卡尺找线的几何、边缘采样和拟合质量参数。</summary>
    public class CaliperLineParams
    {
        /// <summary>获取或设置卡尺主轴起点 X 坐标。</summary>
        public float StartX { get; set; }
        /// <summary>获取或设置卡尺主轴起点 Y 坐标。</summary>
        public float StartY { get; set; }
        /// <summary>获取或设置卡尺主轴终点 X 坐标。</summary>
        public float EndX { get; set; }
        /// <summary>获取或设置卡尺主轴终点 Y 坐标。</summary>
        public float EndY { get; set; }
        /// <summary>获取或设置单把卡尺沿主轴方向的平均宽度。</summary>
        public float CaliperWidth { get; set; } = 20;
        /// <summary>获取或设置单把卡尺沿扫描方向的高度。</summary>
        public float CaliperHeight { get; set; } = 200;
        /// <summary>获取或设置沿主轴布置的卡尺数量。</summary>
        public int Count { get; set; } = 15;
        /// <summary>获取或设置一维梯度边缘强度下限。</summary>
        public int EdgeStrength { get; set; } = 20;
        /// <summary>获取或设置边缘极性。</summary>
        public CaliperEdgePolarity Polarity { get; set; } = CaliperEdgePolarity.Both;
        /// <summary>获取或设置多候选边缘的选择方式。</summary>
        public CaliperEdgeFindMode FindMode { get; set; } = CaliperEdgeFindMode.Best;
        /// <summary>获取或设置扫描方向，零表示垂直主轴。</summary>
        public int Direction { get; set; }
        /// <summary>获取或设置灰度剖面采样模式。</summary>
        public CaliperSamplingMode SamplingMode { get; set; } = CaliperSamplingMode.Fast;
        /// <summary>获取或设置边缘定位精度，默认使用亚像素插值。</summary>
        public GeometryMeasureMode MeasureMode { get; set; } = GeometryMeasureMode.SubPixel;
        /// <summary>获取或设置一维灰度剖面平滑核大小。</summary>
        public int BlurSize { get; set; } = 3;
        /// <summary>获取或设置是否启用直线拟合质量判定。</summary>
        public bool EnableQualityValidation { get; set; } = true;
        /// <summary>获取或设置内点数量占请求卡尺数量的最低比例。</summary>
        public float MinimumValidPointRatio { get; set; } = 0.8f;
        /// <summary>获取或设置内点到拟合线的平均残差上限，单位为像素。</summary>
        public float MaximumAverageResidual { get; set; } = 1.5f;
        /// <summary>获取或设置内点到拟合线的最大残差上限，单位为像素。</summary>
        public float MaximumResidual { get; set; } = 3.5f;
        /// <summary>获取或设置有效内点覆盖卡尺主轴的最低比例。</summary>
        public float MinimumCoverageRatio { get; set; } = 0.75f;
        /// <summary>获取或设置拟合线相对卡尺主轴的最大方向偏差，单位为度。</summary>
        public float MaximumAngleDeviationDegrees { get; set; } = 5.0f;
    }

    /// <summary>
    /// 表示一次卡尺找线的输出、质量统计和失败原因。
    /// </summary>
    public class CaliperLineMeasureResult
    {
        /// <summary>获取或设置鲁棒拟合最终保留的边缘内点。</summary>
        public List<PointF> EdgePoints { get; set; } = new List<PointF>();
        /// <summary>获取或设置拟合线起点。</summary>
        public PointF LineStart { get; set; }
        /// <summary>获取或设置拟合线终点。</summary>
        public PointF LineEnd { get; set; }
        /// <summary>获取或设置找线和质量判定是否成功。</summary>
        public bool Success { get; set; }
        /// <summary>获取或设置失败时的简体中文原因。</summary>
        public string ErrorMessage { get; set; } = string.Empty;
        /// <summary>获取或设置直线拟合质量统计。</summary>
        public CaliperLineFitQuality Quality { get; set; } = new CaliperLineFitQuality();
        /// <summary>获取或设置算法耗时，单位为毫秒。</summary>
        public double AlgorithmMs { get; set; }
        /// <summary>获取鲁棒拟合最终保留的边缘内点数量。</summary>
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
        /// <summary>获取或设置灰度剖面采样模式。</summary>
        public CaliperSamplingMode SamplingMode { get; set; } = CaliperSamplingMode.Fast;
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
        /// <summary>获取或设置灰度剖面采样模式。</summary>
        public CaliperSamplingMode SamplingMode { get; set; } = CaliperSamplingMode.Fast;
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

    /// <summary>提供直线、圆和椭圆的公共卡尺测量算法。</summary>
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

        /// <summary>使用默认质量评估策略执行卡尺找线。</summary>
        public static CaliperLineMeasureResult FindLine(Mat gray, CaliperLineParams p)
        {
            return FindLine(gray, p, DefaultCaliperLineQualityEvaluator.Instance);
        }

        /// <summary>使用指定的可替换质量评估策略执行卡尺找线。</summary>
        /// <param name="gray">单通道灰度图像。</param>
        /// <param name="p">卡尺找线参数。</param>
        /// <param name="qualityEvaluator">直线拟合质量评估策略。</param>
        /// <returns>包含拟合直线、内点和质量统计的测量结果。</returns>
        public static CaliperLineMeasureResult FindLine(
            Mat gray,
            CaliperLineParams p,
            ICaliperLineQualityEvaluator qualityEvaluator)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = new CaliperLineMeasureResult();
            ValidateGray(gray);
            if (p == null)
                throw new ArgumentNullException(nameof(p));
            if (qualityEvaluator == null)
                throw new ArgumentNullException(nameof(qualityEvaluator));

            float axisX = p.EndX - p.StartX;
            float axisY = p.EndY - p.StartY;
            float axisLen = (float)Math.Sqrt(axisX * axisX + axisY * axisY);
            if (axisLen < 1)
            {
                result.ErrorMessage = "卡尺主轴长度不足一个像素。";
                return Finish(result, sw);
            }

            float uAx = axisX / axisLen;
            float uAy = axisY / axisLen;
            float uPerpX = -uAy;
            float uPerpY = uAx;
            float scanDirX = p.Direction == 0 ? uPerpX : uAx;
            float scanDirY = p.Direction == 0 ? uPerpY : uAy;
            float scanRange = p.Direction == 0 ? p.CaliperHeight : p.CaliperWidth;
            float averageDirX = p.Direction == 0 ? uAx : uPerpX;
            float averageDirY = p.Direction == 0 ? uAy : uPerpY;
            float averageWidth = p.Direction == 0 ? p.CaliperWidth : p.CaliperHeight;

            int count = Math.Max(1, p.Count);
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0.5f : (float)i / (count - 1);
                float cx = p.StartX + axisX * t;
                float cy = p.StartY + axisY * t;
                PointF? edgePoint = FindEdgeOnProfile(gray, cx, cy, scanDirX, scanDirY, scanRange, averageDirX, averageDirY, averageWidth, p.SamplingMode, p.MeasureMode, p.BlurSize, p.EdgeStrength, p.Polarity, p.FindMode);
                if (edgePoint.HasValue)
                    result.EdgePoints.Add(edgePoint.Value);
            }

            if (result.EdgePoints.Count < 2)
            {
                result.ErrorMessage = $"有效边缘点数量不足：{result.EdgePoints.Count}/{count}。";
                return Finish(result, sw);
            }

            result.Success = FitLine(result, p, qualityEvaluator);

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
                float averageDirX = -scanDirY;
                float averageDirY = scanDirX;

                PointF? edgePoint = FindEdgeOnProfile(gray, cx, cy, scanDirX, scanDirY, p.CaliperHeight, averageDirX, averageDirY, p.CaliperWidth, p.SamplingMode, GeometryMeasureMode.SubPixel, p.BlurSize, p.EdgeStrength, p.Polarity, p.FindMode);
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
                float averageDirX = -scanDirY;
                float averageDirY = scanDirX;

                PointF? edgePoint = FindEdgeOnProfile(gray, cx, cy, scanDirX, scanDirY, p.CaliperHeight, averageDirX, averageDirY, p.CaliperWidth, p.SamplingMode, GeometryMeasureMode.SubPixel, p.BlurSize, p.EdgeStrength, p.Polarity, p.FindMode);
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
            float averageDirX,
            float averageDirY,
            float averageWidth,
            CaliperSamplingMode samplingMode,
            GeometryMeasureMode measureMode,
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

                // 抗干扰模式执行全宽度平均；快速模式只取三个代表点，在稳定性与速度之间取平衡。
                profile[j] = samplingMode == CaliperSamplingMode.AntiInterference
                    ? SampleAveragedPixel(gray, px, py, averageDirX, averageDirY, averageWidth)
                    : SampleFastPixel(gray, px, py, averageDirX, averageDirY, averageWidth);
            }

            if (blurSize > 1)
                profile = SmoothProfile(profile, blurSize);

            float[] gradient = new float[profileLen];
            for (int j = 1; j < profileLen - 1; j++)
                gradient[j] = (profile[j + 1] - profile[j - 1]) / 2f;

            return FindEdgeInProfile(gradient, posX, posY, measureMode, edgeStrength, polarity, findMode);
        }

        /// <summary>
        /// 使用中心与宽度两侧共三个双线性样本，减少物料旋转和局部反光引起的角度漂移。
        /// </summary>
        private static float SampleFastPixel(
            Mat gray,
            float x,
            float y,
            float averageDirX,
            float averageDirY,
            float averageWidth)
        {
            float halfOffset = Math.Min(4f, Math.Max(0f, averageWidth / 2f));
            if (halfOffset < 0.5f)
            {
                float centerValue = SamplePixelBilinear(gray, x, y);
                return centerValue < 0 ? 0 : centerValue;
            }

            float sum = 0;
            int count = 0;
            for (int sampleIndex = -1; sampleIndex <= 1; sampleIndex++)
            {
                float offset = sampleIndex * halfOffset;
                float value = SamplePixelBilinear(
                    gray,
                    x + averageDirX * offset,
                    y + averageDirY * offset);
                if (value < 0)
                    continue;

                sum += value;
                count++;
            }

            return count == 0 ? 0 : sum / count;
        }

        /// <summary>
        /// 沿卡尺宽度方向做灰度平均，减少金属反光和单像素毛刺对一维边缘剖面的影响。
        /// </summary>
        private static float SampleAveragedPixel(Mat gray, float centerX, float centerY, float averageDirX, float averageDirY, float averageWidth)
        {
            int halfWidth = Math.Min(15, Math.Max(0, (int)Math.Round(Math.Max(0, averageWidth) / 2.0)));
            if (halfWidth == 0)
            {
                float value = SamplePixelBilinear(gray, centerX, centerY);
                return value < 0 ? 0 : value;
            }

            float sum = 0;
            int count = 0;
            for (int offset = -halfWidth; offset <= halfWidth; offset++)
            {
                float value = SamplePixelBilinear(gray, centerX + averageDirX * offset, centerY + averageDirY * offset);
                if (value < 0)
                    continue;

                sum += value;
                count++;
            }

            return count == 0 ? 0 : sum / count;
        }

        /// <summary>
        /// 双线性读取灰度，避免卡尺方向不是水平/垂直时被整数取整抖动放大。
        /// </summary>
        private static float SamplePixelBilinear(Mat gray, float x, float y)
        {
            int x0 = (int)Math.Floor(x);
            int y0 = (int)Math.Floor(y);
            int x1 = x0 + 1;
            int y1 = y0 + 1;
            if (!IsInside(gray, x0, y0) || !IsInside(gray, x1, y1))
                return -1;

            float fx = x - x0;
            float fy = y - y0;
            float v00 = gray.At<byte>(y0, x0);
            float v10 = gray.At<byte>(y0, x1);
            float v01 = gray.At<byte>(y1, x0);
            float v11 = gray.At<byte>(y1, x1);
            float top = v00 * (1 - fx) + v10 * fx;
            float bottom = v01 * (1 - fx) + v11 * fx;
            return top * (1 - fy) + bottom * fy;
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
            GeometryMeasureMode measureMode,
            int threshold,
            CaliperEdgePolarity polarity,
            CaliperEdgeFindMode mode)
        {
            var candidates = new List<EdgeCandidate>();
            var fallbackCandidates = new List<EdgeCandidate>();
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

                fallbackCandidates.Add(new EdgeCandidate(i, absG));
                if (absG >= Math.Abs(gradient[i - 1]) && absG >= Math.Abs(gradient[i + 1]))
                    candidates.Add(new EdgeCandidate(i, absG));
            }

            if (candidates.Count == 0)
                candidates = fallbackCandidates;
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

            // 像素模式直接采用梯度峰值所在的整步采样位置；亚像素模式才执行抛物线峰值插值。
            if (measureMode != GeometryMeasureMode.Pixel && bestIdx > 0 && bestIdx < gradient.Length - 1)
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

        /// <summary>
        /// 使用确定性两点共识搜索和可替换质量评估器完成鲁棒直线拟合。
        /// </summary>
        private static bool FitLine(
            CaliperLineMeasureResult result,
            CaliperLineParams parameters,
            ICaliperLineQualityEvaluator qualityEvaluator)
        {
            List<PointF> candidatePoints = result.EdgePoints == null
                ? new List<PointF>()
                : result.EdgePoints.ToList();
            if (candidatePoints.Count < 2)
            {
                result.Quality = new CaliperLineFitQuality
                {
                    CandidatePointCount = candidatePoints.Count,
                    InlierPointCount = candidatePoints.Count,
                    ValidPointRatio = (double)candidatePoints.Count / Math.Max(1, parameters.Count)
                };
                result.ErrorMessage = $"有效边缘点数量不足：{candidatePoints.Count}/{Math.Max(1, parameters.Count)}。";
                return false;
            }

            double consensusDistance = ResolveConsensusDistance(parameters);
            List<PointF> inlierPoints = BuildConsensusLinePoints(candidatePoints, parameters, consensusDistance);
            if (inlierPoints.Count < 2)
            {
                result.Quality = new CaliperLineFitQuality
                {
                    CandidatePointCount = candidatePoints.Count,
                    InlierPointCount = inlierPoints.Count,
                    ValidPointRatio = (double)inlierPoints.Count / Math.Max(1, parameters.Count)
                };
                result.EdgePoints = inlierPoints;
                result.ErrorMessage = "没有找到至少两个方向一致的共线点。";
                return false;
            }

            RobustLineFit fit = FitLineCore(inlierPoints);
            if (!fit.IsValid)
            {
                result.ErrorMessage = "鲁棒直线拟合失败。";
                return false;
            }

            // 使用第一次拟合线重新吸收全部候选内点，再做一次最小二乘精修，避免两点种子决定最终结果。
            List<PointF> refinedPoints = candidatePoints
                .Where(point => DistanceToLine(point, fit.Start, fit.End) <= consensusDistance)
                .ToList();
            if (refinedPoints.Count >= 2)
            {
                RobustLineFit refinedFit = FitLineCore(refinedPoints);
                if (refinedFit.IsValid)
                {
                    inlierPoints = refinedPoints;
                    fit = refinedFit;
                }
            }

            CaliperLineQualityEvaluation evaluation = qualityEvaluator.Evaluate(new CaliperLineQualityContext
            {
                Parameters = parameters,
                CandidatePoints = candidatePoints,
                InlierPoints = inlierPoints,
                LineStart = fit.Start,
                LineEnd = fit.End
            });

            result.EdgePoints = inlierPoints;
            result.LineStart = fit.Start;
            result.LineEnd = fit.End;
            result.Quality = evaluation.Quality;
            result.ErrorMessage = evaluation.ErrorMessage;
            return evaluation.IsAccepted;
        }

        /// <summary>
        /// 枚举候选点对建立直线种子，选择内点最多、覆盖最长且残差最小的确定性共识集合。
        /// </summary>
        private static List<PointF> BuildConsensusLinePoints(
            IReadOnlyList<PointF> candidatePoints,
            CaliperLineParams parameters,
            double consensusDistance)
        {
            var bestPoints = new List<PointF>();
            double bestCoverage = -1;
            double bestAverageResidual = double.MaxValue;
            if (candidatePoints == null || candidatePoints.Count < 2)
                return bestPoints;

            for (int firstIndex = 0; firstIndex < candidatePoints.Count - 1; firstIndex++)
            {
                for (int secondIndex = firstIndex + 1; secondIndex < candidatePoints.Count; secondIndex++)
                {
                    PointF seedStart = candidatePoints[firstIndex];
                    PointF seedEnd = candidatePoints[secondIndex];
                    if (DistanceBetween(seedStart, seedEnd) < 1.0)
                        continue;
                    if (!IsDirectionAccepted(parameters, seedStart, seedEnd))
                        continue;

                    List<PointF> currentPoints = candidatePoints
                        .Where(point => DistanceToLine(point, seedStart, seedEnd) <= consensusDistance)
                        .ToList();
                    if (currentPoints.Count < 2)
                        continue;

                    RobustLineFit currentFit = FitLineCore(currentPoints);
                    if (!currentFit.IsValid || !IsDirectionAccepted(parameters, currentFit.Start, currentFit.End))
                        continue;

                    double coverage = DistanceBetween(currentFit.Start, currentFit.End);
                    double averageResidual = currentPoints
                        .Average(point => DistanceToLine(point, currentFit.Start, currentFit.End));
                    if (IsBetterConsensus(
                        currentPoints.Count,
                        coverage,
                        averageResidual,
                        bestPoints.Count,
                        bestCoverage,
                        bestAverageResidual))
                    {
                        bestPoints = currentPoints;
                        bestCoverage = coverage;
                        bestAverageResidual = averageResidual;
                    }
                }
            }

            return bestPoints;
        }

        /// <summary>
        /// 按内点数量、覆盖长度和平均残差依次比较两个共识集合。
        /// </summary>
        private static bool IsBetterConsensus(
            int pointCount,
            double coverage,
            double averageResidual,
            int bestPointCount,
            double bestCoverage,
            double bestAverageResidual)
        {
            if (pointCount != bestPointCount)
                return pointCount > bestPointCount;
            if (Math.Abs(coverage - bestCoverage) > 1e-6)
                return coverage > bestCoverage;
            return averageResidual < bestAverageResidual;
        }

        /// <summary>
        /// 从质量残差门限派生共识距离，避免错误点集把自适应阈值一同拉大。
        /// </summary>
        private static double ResolveConsensusDistance(CaliperLineParams parameters)
        {
            double averageLimit = parameters.MaximumAverageResidual;
            if (double.IsNaN(averageLimit) || double.IsInfinity(averageLimit) || averageLimit <= 0)
                averageLimit = 1.5;
            double maximumLimit = parameters.MaximumResidual;
            if (double.IsNaN(maximumLimit) || double.IsInfinity(maximumLimit) || maximumLimit <= 0)
                maximumLimit = 3.5;

            return Math.Max(0.75, Math.Min(maximumLimit, averageLimit * 1.5));
        }

        /// <summary>
        /// 检查拟合线方向是否符合卡尺主轴；沿主轴扫描模式不施加该约束。
        /// </summary>
        private static bool IsDirectionAccepted(CaliperLineParams parameters, PointF lineStart, PointF lineEnd)
        {
            if (parameters.Direction != 0 || !parameters.EnableQualityValidation)
                return true;

            double maximumDeviation = parameters.MaximumAngleDeviationDegrees;
            if (double.IsNaN(maximumDeviation) || double.IsInfinity(maximumDeviation) || maximumDeviation <= 0)
                maximumDeviation = 5.0;

            double expected = Math.Atan2(parameters.EndY - parameters.StartY, parameters.EndX - parameters.StartX);
            double actual = Math.Atan2(lineEnd.Y - lineStart.Y, lineEnd.X - lineStart.X);
            double difference = Math.Abs((actual - expected) * 180.0 / Math.PI) % 180.0;
            difference = difference > 90.0 ? 180.0 - difference : difference;
            return difference <= maximumDeviation + 1e-9;
        }

        /// <summary>
        /// 使用 OpenCV 拟合直线，并把无限线截取到输入点投影范围。
        /// </summary>
        private static RobustLineFit FitLineCore(IList<PointF> points)
        {
            if (points == null || points.Count < 2)
                return new RobustLineFit();

            Point2f[] cvPts = points.Select(p => new Point2f(p.X, p.Y)).ToArray();
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

                foreach (PointF point in points)
                {
                    float t = (point.X - x0) * vx + (point.Y - y0) * vy;
                    minT = Math.Min(minT, t);
                    maxT = Math.Max(maxT, t);
                }

                PointF start = new PointF(x0 + vx * minT, y0 + vy * minT);
                PointF end = new PointF(x0 + vx * maxT, y0 + vy * maxT);
                return new RobustLineFit
                {
                    IsValid = DistanceBetween(start, end) > 0.0001,
                    Start = start,
                    End = end
                };
            }
        }

        /// <summary>
        /// 计算两点间距离。
        /// </summary>
        private static double DistanceBetween(PointF p1, PointF p2)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// 计算点到直线的垂直距离。
        /// </summary>
        private static double DistanceToLine(PointF point, PointF lineStart, PointF lineEnd)
        {
            double dx = lineEnd.X - lineStart.X;
            double dy = lineEnd.Y - lineStart.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);
            if (length < 1e-8)
                return double.MaxValue;

            return Math.Abs((point.X - lineStart.X) * dy - (point.Y - lineStart.Y) * dx) / length;
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

        /// <summary>
        /// 直线拟合中间结果。
        /// </summary>
        private struct RobustLineFit
        {
            /// <summary>
            /// 拟合结果是否有效。
            /// </summary>
            public bool IsValid { get; set; }

            /// <summary>
            /// 拟合线段起点。
            /// </summary>
            public PointF Start { get; set; }

            /// <summary>
            /// 拟合线段终点。
            /// </summary>
            public PointF End { get; set; }
        }
    }
}
