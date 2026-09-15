using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace TDJS_Vision.Node._4_Measurement.Common
{
    /// <summary>
    /// 描述卡尺直线拟合的质量统计，供算法判定、结果显示和下游订阅复用。
    /// </summary>
    public sealed class CaliperLineFitQuality
    {
        /// <summary>获取或设置扫描得到的候选边缘点数量。</summary>
        public int CandidatePointCount { get; set; }

        /// <summary>获取或设置鲁棒拟合最终保留的内点数量。</summary>
        public int InlierPointCount { get; set; }

        /// <summary>获取或设置内点数量占请求卡尺数量的比例。</summary>
        public double ValidPointRatio { get; set; }

        /// <summary>获取或设置内点到拟合线的平均垂直距离，单位为像素。</summary>
        public double AverageResidual { get; set; }

        /// <summary>获取或设置内点到拟合线的最大垂直距离，单位为像素。</summary>
        public double MaximumResidual { get; set; }

        /// <summary>获取或设置内点投影跨度占卡尺主轴长度的比例。</summary>
        public double CoverageRatio { get; set; }

        /// <summary>获取或设置拟合线与卡尺主轴之间的最小方向偏差，单位为度。</summary>
        public double AngleDeviationDegrees { get; set; }
    }

    /// <summary>
    /// 提供卡尺直线质量判定所需的不可变上下文。
    /// </summary>
    public sealed class CaliperLineQualityContext
    {
        /// <summary>获取或设置卡尺算法参数。</summary>
        public CaliperLineParams Parameters { get; set; }

        /// <summary>获取或设置扫描得到的全部候选边缘点。</summary>
        public IReadOnlyList<PointF> CandidatePoints { get; set; }

        /// <summary>获取或设置鲁棒拟合保留的内点。</summary>
        public IReadOnlyList<PointF> InlierPoints { get; set; }

        /// <summary>获取或设置最终拟合线起点。</summary>
        public PointF LineStart { get; set; }

        /// <summary>获取或设置最终拟合线终点。</summary>
        public PointF LineEnd { get; set; }
    }

    /// <summary>
    /// 描述一次卡尺直线质量判定结果。
    /// </summary>
    public sealed class CaliperLineQualityEvaluation
    {
        /// <summary>获取或设置质量是否满足当前门限。</summary>
        public bool IsAccepted { get; set; }

        /// <summary>获取或设置拒绝当前拟合线时的简体中文原因。</summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>获取或设置本次拟合的完整质量统计。</summary>
        public CaliperLineFitQuality Quality { get; set; } = new CaliperLineFitQuality();
    }

    /// <summary>
    /// 定义可替换的卡尺直线质量评估策略。
    /// </summary>
    public interface ICaliperLineQualityEvaluator
    {
        /// <summary>
        /// 计算拟合质量，并按照参数门限决定是否允许输出该直线。
        /// </summary>
        /// <param name="context">待评估的卡尺直线上下文。</param>
        /// <returns>包含质量统计和拒绝原因的判定结果。</returns>
        CaliperLineQualityEvaluation Evaluate(CaliperLineQualityContext context);
    }

    /// <summary>
    /// 默认卡尺直线质量评估器，通过有效点、残差、覆盖率和方向一致性阻止错误点集继续输出。
    /// </summary>
    public sealed class DefaultCaliperLineQualityEvaluator : ICaliperLineQualityEvaluator
    {
        /// <summary>获取无状态默认评估器的共享实例。</summary>
        public static readonly DefaultCaliperLineQualityEvaluator Instance = new DefaultCaliperLineQualityEvaluator();

        /// <summary>防止外部重复创建无状态评估器。</summary>
        private DefaultCaliperLineQualityEvaluator()
        {
        }

        /// <inheritdoc />
        public CaliperLineQualityEvaluation Evaluate(CaliperLineQualityContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (context.Parameters == null)
                throw new ArgumentException("卡尺质量判定参数为空。", nameof(context));

            CaliperLineParams parameters = context.Parameters;
            IReadOnlyList<PointF> candidatePoints = context.CandidatePoints ?? Array.Empty<PointF>();
            IReadOnlyList<PointF> inlierPoints = context.InlierPoints ?? Array.Empty<PointF>();
            var quality = BuildQuality(parameters, candidatePoints, inlierPoints, context.LineStart, context.LineEnd);
            var evaluation = new CaliperLineQualityEvaluation
            {
                IsAccepted = true,
                Quality = quality
            };

            if (!parameters.EnableQualityValidation)
                return evaluation;

            double minimumValidPointRatio = NormalizeRatio(parameters.MinimumValidPointRatio, 0.8);
            double maximumAverageResidual = NormalizePositive(parameters.MaximumAverageResidual, 1.5);
            double maximumResidual = NormalizePositive(parameters.MaximumResidual, 3.5);
            double minimumCoverageRatio = NormalizeRatio(parameters.MinimumCoverageRatio, 0.75);
            double maximumAngleDeviation = NormalizePositive(parameters.MaximumAngleDeviationDegrees, 5.0);

            if (quality.ValidPointRatio + 1e-9 < minimumValidPointRatio)
                return Reject(evaluation, $"有效点比例不足：{quality.ValidPointRatio:P1}，要求不低于{minimumValidPointRatio:P1}。");
            if (quality.AverageResidual > maximumAverageResidual + 1e-9)
                return Reject(evaluation, $"平均拟合残差过大：{quality.AverageResidual:F3}px，上限{maximumAverageResidual:F3}px。");
            if (quality.MaximumResidual > maximumResidual + 1e-9)
                return Reject(evaluation, $"最大拟合残差过大：{quality.MaximumResidual:F3}px，上限{maximumResidual:F3}px。");
            if (quality.CoverageRatio + 1e-9 < minimumCoverageRatio)
                return Reject(evaluation, $"有效点覆盖率不足：{quality.CoverageRatio:P1}，要求不低于{minimumCoverageRatio:P1}。");
            if (parameters.Direction == 0 && quality.AngleDeviationDegrees > maximumAngleDeviation + 1e-9)
                return Reject(evaluation, $"拟合线方向偏差过大：{quality.AngleDeviationDegrees:F3}°，上限{maximumAngleDeviation:F3}°。");

            return evaluation;
        }

        /// <summary>
        /// 计算全部质量指标，质量判定关闭时仍然保留这些统计供诊断使用。
        /// </summary>
        private static CaliperLineFitQuality BuildQuality(
            CaliperLineParams parameters,
            IReadOnlyList<PointF> candidatePoints,
            IReadOnlyList<PointF> inlierPoints,
            PointF lineStart,
            PointF lineEnd)
        {
            var residuals = inlierPoints.Select(point => DistanceToLine(point, lineStart, lineEnd)).ToList();
            int requestedCount = Math.Max(1, parameters.Count);
            double axisLength = DistanceBetween(
                new PointF(parameters.StartX, parameters.StartY),
                new PointF(parameters.EndX, parameters.EndY));
            double fittedSpan = DistanceBetween(lineStart, lineEnd);

            return new CaliperLineFitQuality
            {
                CandidatePointCount = candidatePoints.Count,
                InlierPointCount = inlierPoints.Count,
                ValidPointRatio = Math.Min(1.0, (double)inlierPoints.Count / requestedCount),
                AverageResidual = residuals.Count == 0 ? 0 : residuals.Average(),
                MaximumResidual = residuals.Count == 0 ? 0 : residuals.Max(),
                CoverageRatio = axisLength <= 1e-8 ? 0 : Math.Min(1.0, fittedSpan / axisLength),
                AngleDeviationDegrees = CalculateLineAngleDeviation(parameters, lineStart, lineEnd)
            };
        }

        /// <summary>把当前判定转换为拒绝状态并写入失败原因。</summary>
        private static CaliperLineQualityEvaluation Reject(CaliperLineQualityEvaluation evaluation, string errorMessage)
        {
            evaluation.IsAccepted = false;
            evaluation.ErrorMessage = errorMessage;
            return evaluation;
        }

        /// <summary>计算两条无方向直线之间的最小角度差。</summary>
        private static double CalculateLineAngleDeviation(CaliperLineParams parameters, PointF lineStart, PointF lineEnd)
        {
            double expected = Math.Atan2(parameters.EndY - parameters.StartY, parameters.EndX - parameters.StartX);
            double actual = Math.Atan2(lineEnd.Y - lineStart.Y, lineEnd.X - lineStart.X);
            double difference = Math.Abs((actual - expected) * 180.0 / Math.PI) % 180.0;
            return difference > 90.0 ? 180.0 - difference : difference;
        }

        /// <summary>计算目标点到无限直线的垂直距离。</summary>
        private static double DistanceToLine(PointF point, PointF lineStart, PointF lineEnd)
        {
            double dx = lineEnd.X - lineStart.X;
            double dy = lineEnd.Y - lineStart.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);
            if (length <= 1e-8)
                return double.MaxValue;

            return Math.Abs((point.X - lineStart.X) * dy - (point.Y - lineStart.Y) * dx) / length;
        }

        /// <summary>计算两点之间的欧氏距离。</summary>
        private static double DistanceBetween(PointF first, PointF second)
        {
            double dx = second.X - first.X;
            double dy = second.Y - first.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>把比例限制在零到一范围，无效值回退到默认值。</summary>
        private static double NormalizeRatio(double value, double fallback)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
                return fallback;
            // 单精度方案参数转为双精度时会出现0.8000000119一类尾差，按配置界面精度归一化。
            return Math.Round(Math.Min(1.0, value), 6, MidpointRounding.AwayFromZero);
        }

        /// <summary>确保上限参数为有效正数。</summary>
        private static double NormalizePositive(double value, double fallback)
        {
            return double.IsNaN(value) || double.IsInfinity(value) || value <= 0 ? fallback : value;
        }
    }
}
