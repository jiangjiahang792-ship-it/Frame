using OpenCvSharp;
using TieCornerInspection.WinForms.Models;

namespace TieCornerInspection.WinForms.Services;

/// <summary>
/// 通过自适应列纹理能量定位齿纹区到光滑端头的结构转折。
/// </summary>
public sealed class TextureTransitionEndBoundaryLocator : IRightEndBoundaryLocator
{
    /// <summary>
    /// 右侧光滑端头至少占组件总宽度的比例。
    /// </summary>
    private const double MinimumTailWidthRatio = 0.25;

    /// <inheritdoc />
    public bool TryLocate(
        Mat alignedBinaryComponent,
        Mat alignedColorComponent,
        EndBoundaryLocatorOptions options,
        out EndBoundaryCandidate candidate)
    {
        ValidateInputs(alignedBinaryComponent, alignedColorComponent);
        candidate = default;

        if (alignedBinaryComponent.Empty() || alignedBinaryComponent.Height < 5 || alignedBinaryComponent.Width < 12)
        {
            return false;
        }

        TextureSignal signal = BuildTextureSignal(alignedBinaryComponent, alignedColorComponent);
        int windowWidth = Math.Max(5, (int)Math.Round(alignedBinaryComponent.Height * 0.30));
        int minimumBoundaryX = Math.Max(2, windowWidth * 2);
        int minimumTailWidth = Math.Max(
            alignedBinaryComponent.Height * 2,
            (int)Math.Round(alignedBinaryComponent.Width * MinimumTailWidthRatio));
        int maximumBoundaryX = alignedBinaryComponent.Width - minimumTailWidth;

        return TryFindTransition(
            signal,
            windowWidth,
            minimumBoundaryX,
            maximumBoundaryX,
            GetMinimumTransitionConfidence(options),
            out candidate);
    }

    /// <inheritdoc />
    public bool TryRefineNear(
        Mat alignedBinaryComponent,
        Mat alignedColorComponent,
        int predictedBoundaryX,
        int searchRadius,
        EndBoundaryLocatorOptions options,
        out EndBoundaryCandidate candidate)
    {
        ValidateInputs(alignedBinaryComponent, alignedColorComponent);
        candidate = default;

        if (alignedBinaryComponent.Empty() || alignedBinaryComponent.Height < 5 || alignedBinaryComponent.Width < 12 ||
            searchRadius <= 0)
        {
            return false;
        }

        TextureSignal signal = BuildTextureSignal(alignedBinaryComponent, alignedColorComponent);
        int windowWidth = Math.Max(5, (int)Math.Round(alignedBinaryComponent.Height * 0.30));
        int minimumBoundaryX = Math.Max(windowWidth * 2, predictedBoundaryX - searchRadius);
        int maximumByTail = alignedBinaryComponent.Width - Math.Max(
            alignedBinaryComponent.Height * 2,
            (int)Math.Round(alignedBinaryComponent.Width * MinimumTailWidthRatio));
        int maximumBoundaryX = Math.Min(maximumByTail, predictedBoundaryX + searchRadius);

        return TryFindTransition(
            signal,
            windowWidth,
            minimumBoundaryX,
            maximumBoundaryX,
            GetMinimumTransitionConfidence(options),
            out candidate);
    }

    /// <summary>
    /// 校验二值和彩色组件的类型与几何一致性。
    /// </summary>
    /// <param name="binary">单通道二值组件。</param>
    /// <param name="color">三通道彩色组件。</param>
    private static void ValidateInputs(Mat binary, Mat color)
    {
        ArgumentNullException.ThrowIfNull(binary);
        ArgumentNullException.ThrowIfNull(color);

        if (!binary.Empty() && binary.Type() != MatType.CV_8UC1)
        {
            throw new ArgumentException("端头边界定位器只接受单通道 8 位二值图。", nameof(binary));
        }

        if (!color.Empty() && color.Type() != MatType.CV_8UC3)
        {
            throw new ArgumentException("端头边界定位器只接受三通道 8 位彩色图。", nameof(color));
        }

        if (binary.Size() != color.Size())
        {
            throw new ArgumentException("二值组件和彩色组件必须具有相同尺寸。", nameof(color));
        }
    }

    /// <summary>
    /// 构建不受整体曝光平移影响的一维列纹理信号。
    /// </summary>
    /// <param name="binary">组件前景掩膜。</param>
    /// <param name="color">组件彩色图。</param>
    /// <returns>逐列灰度、相邻差分能量及稳健统计量。</returns>
    private static TextureSignal BuildTextureSignal(Mat binary, Mat color)
    {
        int width = binary.Width;
        int height = binary.Height;
        int projectionTop = (int)Math.Floor(height * 0.25);
        int projectionBottom = Math.Max(projectionTop + 1, (int)Math.Ceiling(height * 0.75));
        double[] profile = new double[width];
        double[] columnValues = new double[projectionBottom - projectionTop];

        for (int x = 0; x < width; x++)
        {
            int count = 0;
            for (int y = projectionTop; y < projectionBottom; y++)
            {
                if (binary.At<byte>(y, x) == 0)
                {
                    continue;
                }

                Vec3b pixel = color.At<Vec3b>(y, x);
                columnValues[count++] = Math.Max(pixel.Item0, Math.Max(pixel.Item1, pixel.Item2));
            }

            profile[x] = count == 0 ? (x == 0 ? 0 : profile[x - 1]) : MedianInPlace(columnValues, count);
        }

        double[] energy = new double[width];
        for (int x = 1; x < width; x++)
        {
            energy[x] = Math.Abs(profile[x] - profile[x - 1]);
        }

        double energyMedian = MedianCopy(energy, energy.Length);
        double[] deviations = new double[energy.Length];
        for (int index = 0; index < energy.Length; index++)
        {
            deviations[index] = Math.Abs(energy[index] - energyMedian);
        }

        double medianAbsoluteDeviation = MedianInPlace(deviations, deviations.Length);
        double[] prefixSum = new double[energy.Length + 1];
        for (int index = 0; index < energy.Length; index++)
        {
            prefixSum[index + 1] = prefixSum[index] + energy[index];
        }

        return new TextureSignal(energy, prefixSum, energyMedian, medianAbsoluteDeviation);
    }

    /// <summary>
    /// 在指定范围内寻找纹理能量由高到低的最可信转折。
    /// </summary>
    /// <param name="signal">一维纹理信号。</param>
    /// <param name="windowWidth">左右统计窗口宽度。</param>
    /// <param name="minimumBoundaryX">允许的最小边界列。</param>
    /// <param name="maximumBoundaryX">允许的最大边界列。</param>
    /// <param name="candidate">成功时的细化边界。</param>
    /// <returns>存在可信转折时为真。</returns>
    private static bool TryFindTransition(
        TextureSignal signal,
        int windowWidth,
        int minimumBoundaryX,
        int maximumBoundaryX,
        double minimumTransitionConfidence,
        out EndBoundaryCandidate candidate)
    {
        candidate = default;
        if (minimumBoundaryX > maximumBoundaryX || minimumBoundaryX < 1 || maximumBoundaryX >= signal.Energy.Length)
        {
            return false;
        }

        int contextWidth = windowWidth * 2;
        double normalization = Math.Max(1.0, signal.EnergyMedian + signal.MedianAbsoluteDeviation);
        int rightmostBoundaryX = -1;
        double rightmostConfidence = 0;

        for (int boundaryX = minimumBoundaryX; boundaryX <= maximumBoundaryX; boundaryX++)
        {
            int leftStart = Math.Max(1, boundaryX - contextWidth);
            int rightEnd = Math.Min(signal.Energy.Length, boundaryX + contextWidth);
            double leftEnergy = Mean(signal.PrefixSum, leftStart, boundaryX);
            double rightEnergy = Mean(signal.PrefixSum, boundaryX, rightEnd);
            double confidence = (leftEnergy - rightEnergy) / normalization;

            if (leftEnergy <= rightEnergy || confidence < minimumTransitionConfidence)
            {
                continue;
            }

            // 用户要求截取从最后一条可信纹路右侧开始，因此位置优先于峰值强度。
            rightmostBoundaryX = boundaryX;
            rightmostConfidence = confidence;
        }

        if (rightmostBoundaryX < 0)
        {
            return false;
        }

        // 可信窗口会在真实末纹之后继续保持一段高分，回查必须覆盖完整左侧上下文，
        // 才能从窗口中心重新吸附到最后一个真实纹理沿。
        int refineStart = Math.Max(minimumBoundaryX, rightmostBoundaryX - contextWidth);
        int refineEnd = Math.Min(maximumBoundaryX, rightmostBoundaryX + windowWidth);
        double significantEdge = Math.Max(
            signal.EnergyMedian + 2.0 * signal.MedianAbsoluteDeviation,
            Max(signal.Energy, refineStart, refineEnd + 1) * 0.30);
        int refinedBoundaryX = -1;

        for (int x = refineEnd; x >= refineStart; x--)
        {
            if (signal.Energy[x] >= significantEdge && signal.Energy[x] > 0)
            {
                refinedBoundaryX = x;
                break;
            }
        }

        if (refinedBoundaryX < 0)
        {
            refinedBoundaryX = rightmostBoundaryX;
        }

        candidate = new EndBoundaryCandidate(refinedBoundaryX, Math.Max(0, rightmostConfidence));
        return true;
    }

    /// <summary>
    /// 把用户可理解的 0～100 灵敏度映射为内部归一化置信度门槛。
    /// </summary>
    /// <param name="options">端头边界定位选项。</param>
    /// <returns>灵敏度越高越低的可信度门槛。</returns>
    private static double GetMinimumTransitionConfidence(EndBoundaryLocatorOptions options)
    {
        return 1.60 - options.TextureSensitivity / 100.0;
    }

    /// <summary>
    /// 对缓存前 count 个值原地排序并计算中位数。
    /// </summary>
    /// <param name="values">数值缓存。</param>
    /// <param name="count">参与统计的有效数值个数。</param>
    /// <returns>中位数。</returns>
    private static double MedianInPlace(double[] values, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        Array.Sort(values, 0, count);
        int middle = count / 2;
        return count % 2 == 1 ? values[middle] : (values[middle - 1] + values[middle]) / 2.0;
    }

    /// <summary>
    /// 复制缓存后计算中位数，避免破坏后续仍要使用的纹理信号。
    /// </summary>
    /// <param name="values">数值缓存。</param>
    /// <param name="count">参与统计的数值个数。</param>
    /// <returns>中位数。</returns>
    private static double MedianCopy(double[] values, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        double[] copy = new double[count];
        Array.Copy(values, copy, count);
        return MedianInPlace(copy, count);
    }

    /// <summary>
    /// 计算半开区间中的最大值，避免在每根扎带上创建 LINQ 临时集合。
    /// </summary>
    /// <param name="values">纹理能量缓存。</param>
    /// <param name="start">起始索引。</param>
    /// <param name="end">结束索引，不包含该位置。</param>
    /// <returns>区间最大值；空区间返回零。</returns>
    private static double Max(double[] values, int start, int end)
    {
        double maximum = 0;
        for (int index = start; index < end; index++)
        {
            maximum = Math.Max(maximum, values[index]);
        }

        return maximum;
    }

    /// <summary>
    /// 使用前缀和计算半开区间的平均值。
    /// </summary>
    /// <param name="prefixSum">纹理能量前缀和。</param>
    /// <param name="start">起始索引。</param>
    /// <param name="end">结束索引，不包含该位置。</param>
    /// <returns>区间平均能量。</returns>
    private static double Mean(double[] prefixSum, int start, int end)
    {
        return end <= start ? 0 : (prefixSum[end] - prefixSum[start]) / (end - start);
    }

    /// <summary>
    /// 保存逐列纹理能量及其稳健统计量。
    /// </summary>
    /// <param name="Energy">相邻列灰度差分绝对值。</param>
    /// <param name="PrefixSum">纹理能量前缀和。</param>
    /// <param name="EnergyMedian">全部能量中位数。</param>
    /// <param name="MedianAbsoluteDeviation">能量中位绝对偏差。</param>
    private sealed record TextureSignal(
        double[] Energy,
        double[] PrefixSum,
        double EnergyMedian,
        double MedianAbsoluteDeviation);
}
