using System;
using System.Drawing;
using System.Threading;
using OpenCvSharp;
using TDJS_Vision.Node._3_Detection.NewContourMatch;
using CvRect = OpenCvSharp.Rect;

namespace TDJS_Vision.Forms.AiTrainForm
{
    /// <summary>
    /// 无监督模板初匹配设置，保存模板、基准位姿和用户要求的整圆搜索参数。
    /// </summary>
    public sealed class UnsupervisedTemplateMatchSettings
    {
        /// <summary>默认匹配数量。</summary>
        public const int DefaultMaximumMatches = 1;
        /// <summary>默认最低匹配分数。</summary>
        public const double DefaultMinimumScore = 0.5D;
        /// <summary>无监督节点运行识别时的默认最低匹配分数。</summary>
        public const double DefaultRecognitionMinimumScore = 0.1D;
        /// <summary>配置层默认起始角度。</summary>
        public const double DefaultAngleStart = -360D;
        /// <summary>配置层默认结束角度。</summary>
        public const double DefaultAngleEnd = 360D;

        /// <summary>新版本轮廓模板参数快照。</summary>
        public NodeParamNewContourMatch Parameters { get; set; }
        /// <summary>基准图模板中心 X。</summary>
        public double BaselineCenterX { get; set; }
        /// <summary>基准图模板中心 Y。</summary>
        public double BaselineCenterY { get; set; }
        /// <summary>基准图模板角度。</summary>
        public double BaselineAngle { get; set; }
        /// <summary>配置层搜索起始角度。</summary>
        public double AngleStart { get; set; } = DefaultAngleStart;
        /// <summary>配置层搜索结束角度。</summary>
        public double AngleEnd { get; set; } = DefaultAngleEnd;
    }

    /// <summary>
    /// 复用新版本轮廓模板匹配节点，为训练和生产识别提供完全一致的 ROI 跟随裁剪。
    /// </summary>
    internal static class UnsupervisedTemplateRoiMatcher
    {
        /// <summary>
        /// 创建固定为单目标、0.5 分数、整圆搜索的运行参数副本。
        /// 执行层按配置直接使用 -360～360 度完整搜索范围。
        /// </summary>
        internal static NodeParamNewContourMatch CreateRuntimeParameters(NodeParamNewContourMatch source)
        {
            return CreateRuntimeParameters(source, UnsupervisedTemplateMatchSettings.DefaultMinimumScore);
        }

        /// <summary>创建无监督节点运行识别参数，固定单目标和0.1最低分数。</summary>
        internal static NodeParamNewContourMatch CreateRecognitionParameters(NodeParamNewContourMatch source)
        {
            return CreateRuntimeParameters(source, UnsupervisedTemplateMatchSettings.DefaultRecognitionMinimumScore);
        }

        /// <summary>按指定最低分数创建单目标整圆搜索参数。</summary>
        private static NodeParamNewContourMatch CreateRuntimeParameters(NodeParamNewContourMatch source, double minimumScore)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            NodeParamNewContourMatch result = source.Copy();
            result.AllSearch = true;
            result.FindOptions = result.FindOptions ?? new FindOptions();
            result.FindOptions.MaximumMatches = UnsupervisedTemplateMatchSettings.DefaultMaximumMatches;
            result.FindOptions.MinimumScore = minimumScore;
            result.FindOptions.AngleStartDegrees = UnsupervisedTemplateMatchSettings.DefaultAngleStart;
            result.FindOptions.AngleEndDegrees = UnsupervisedTemplateMatchSettings.DefaultAngleEnd;
            return result;
        }

        /// <summary>在基准图上执行一次匹配并生成后续 ROI 跟随所需设置。</summary>
        internal static UnsupervisedTemplateMatchSettings CreateSettings(Mat baselineImage, NodeParamNewContourMatch parameters)
        {
            NodeParamNewContourMatch runtime = CreateRuntimeParameters(parameters);
            ShapeMatchResult match = MatchFirst(baselineImage, runtime);
            return new UnsupervisedTemplateMatchSettings
            {
                Parameters = runtime,
                BaselineCenterX = match.CenterX,
                BaselineCenterY = match.CenterY,
                BaselineAngle = match.AngleDegrees
            };
        }

        /// <summary>先匹配模板，再把当前位姿校正到基准位姿并截取基准 ROI。</summary>
        internal static Mat Crop(Mat source, CvRect baselineRoi, UnsupervisedTemplateMatchSettings settings)
        {
            using (var session = new NewContourMatchSession())
            {
                return Crop(source, baselineRoi, settings, session);
            }
        }

        /// <summary>使用调用方持有的匹配会话裁剪，连续图片可复用已恢复的原生模板缓存。</summary>
        internal static Mat Crop(Mat source, CvRect baselineRoi, UnsupervisedTemplateMatchSettings settings, NewContourMatchSession session)
        {
            RotatedRect ignoredRoi;
            return Crop(source, baselineRoi, settings, session, out ignoredRoi);
        }

        /// <summary>匹配并裁剪 ROI，同时返回当前源图坐标系中的仿射 ROI。</summary>
        internal static Mat Crop(Mat source, CvRect baselineRoi, UnsupervisedTemplateMatchSettings settings, NewContourMatchSession session, out RotatedRect affineRoi)
        {
            if (source == null || source.Empty()) throw new ArgumentException("模板初匹配输入图像为空。", nameof(source));
            if (settings?.Parameters == null) throw new InvalidOperationException("模板初匹配参数不完整。请重新训练模板。");
            CvRect roi = ClampRect(baselineRoi, source.Width, source.Height);
            if (roi.Width <= 0 || roi.Height <= 0) throw new InvalidOperationException("模板跟随 ROI 超出输入图像范围。");

            if (session == null) throw new ArgumentNullException(nameof(session));
            ShapeMatchResult current = MatchFirst(source, settings.Parameters, session);
            affineRoi = TransformBaselineRoi(baselineRoi, settings, current);
            double radians = (settings.BaselineAngle - current.AngleDegrees) * Math.PI / 180D;
            double cosine = Math.Cos(radians);
            double sine = Math.Sin(radians);
            double translateX = settings.BaselineCenterX - cosine * current.CenterX + sine * current.CenterY;
            double translateY = settings.BaselineCenterY - sine * current.CenterX - cosine * current.CenterY;
            using (var matrix = new Mat(2, 3, MatType.CV_64FC1))
            using (var aligned = new Mat())
            {
                matrix.Set(0, 0, cosine); matrix.Set(0, 1, -sine); matrix.Set(0, 2, translateX);
                matrix.Set(1, 0, sine); matrix.Set(1, 1, cosine); matrix.Set(1, 2, translateY);
                Cv2.WarpAffine(source, aligned, matrix, source.Size(), InterpolationFlags.Linear, BorderTypes.Constant, Scalar.Black);
                return new Mat(aligned, roi).Clone();
            }
        }

        /// <summary>仅定位模板并返回当前源图中的仿射 ROI，不裁剪图像。</summary>
        internal static RotatedRect LocateAffineRoi(Mat source, CvRect baselineRoi, UnsupervisedTemplateMatchSettings settings, NewContourMatchSession session)
        {
            if (source == null || source.Empty()) throw new ArgumentException("模板初匹配输入图像为空。", nameof(source));
            if (settings?.Parameters == null) throw new InvalidOperationException("模板初匹配参数不完整。请重新训练模板。");
            if (session == null) throw new ArgumentNullException(nameof(session));
            ShapeMatchResult current = MatchFirst(source, settings.Parameters, session);
            return TransformBaselineRoi(baselineRoi, settings, current);
        }

        /// <summary>将基准 ROI 按模板当前位姿变换到源图坐标系。</summary>
        private static RotatedRect TransformBaselineRoi(CvRect baselineRoi, UnsupervisedTemplateMatchSettings settings, ShapeMatchResult current)
        {
            double deltaDegrees = current.AngleDegrees - settings.BaselineAngle;
            double radians = deltaDegrees * Math.PI / 180D;
            double cosine = Math.Cos(radians);
            double sine = Math.Sin(radians);
            double baselineCenterX = baselineRoi.X + baselineRoi.Width / 2D;
            double baselineCenterY = baselineRoi.Y + baselineRoi.Height / 2D;
            double relativeX = baselineCenterX - settings.BaselineCenterX;
            double relativeY = baselineCenterY - settings.BaselineCenterY;
            float centerX = (float)(current.CenterX + cosine * relativeX - sine * relativeY);
            float centerY = (float)(current.CenterY + sine * relativeX + cosine * relativeY);
            return new RotatedRect(
                new Point2f(centerX, centerY),
                new Size2f(baselineRoi.Width, baselineRoi.Height),
                (float)deltaDegrees);
        }

        /// <summary>执行新版本轮廓匹配并返回唯一的最高分目标。</summary>
        private static ShapeMatchResult MatchFirst(Mat image, NodeParamNewContourMatch parameters)
        {
            using (var session = new NewContourMatchSession())
            {
                return MatchFirst(image, parameters, session);
            }
        }

        /// <summary>通过可复用会话执行匹配，避免每帧重新恢复原生模板。</summary>
        private static ShapeMatchResult MatchFirst(Mat image, NodeParamNewContourMatch parameters, NewContourMatchSession session)
        {
            NewContourMatchExecution execution = session.Execute(image, parameters, CancellationToken.None);
            if (execution.Matches == null || execution.Matches.Count == 0)
                throw new InvalidOperationException(
                    "模板初匹配失败：未找到分数不低于 " +
                    parameters.FindOptions.MinimumScore.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                    " 的目标。");
            return execution.Matches[0];
        }

        /// <summary>限制 ROI 位于输入图像内。</summary>
        private static CvRect ClampRect(CvRect rect, int width, int height)
        {
            int x = Math.Max(0, Math.Min(rect.X, width - 1));
            int y = Math.Max(0, Math.Min(rect.Y, height - 1));
            return new CvRect(x, y, Math.Max(0, Math.Min(rect.Width, width - x)), Math.Max(0, Math.Min(rect.Height, height - y)));
        }
    }
}
