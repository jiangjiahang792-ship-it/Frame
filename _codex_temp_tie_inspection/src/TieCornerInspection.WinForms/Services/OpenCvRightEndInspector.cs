using OpenCvSharp;
using TieCornerInspection.WinForms.Models;
using CvPoint = OpenCvSharp.Point;
using CvSize = OpenCvSharp.Size;

namespace TieCornerInspection.WinForms.Services;

/// <summary>
/// 使用 OpenCvSharp 完成黑色提取、连通域分割、齿纹边界定位和端头区域裁剪。
/// </summary>
public sealed class OpenCvRightEndInspector : IRightEndInspector
{
    /// <summary>
    /// 保存可替换的齿纹结束位置定位策略。
    /// </summary>
    private readonly IRightEndBoundaryLocator _boundaryLocator;

    /// <summary>
    /// 使用默认自适应纹理转折定位器创建检测器。
    /// </summary>
    public OpenCvRightEndInspector()
        : this(new TextureTransitionEndBoundaryLocator())
    {
    }

    /// <summary>
    /// 使用指定端头边界定位策略创建检测器。
    /// </summary>
    /// <param name="boundaryLocator">齿纹结束位置定位策略。</param>
    public OpenCvRightEndInspector(IRightEndBoundaryLocator boundaryLocator)
    {
        _boundaryLocator = boundaryLocator ?? throw new ArgumentNullException(nameof(boundaryLocator));
    }

    /// <summary>
    /// 在指定图像和参数上执行黑色连通域分割与端头区域截取。
    /// </summary>
    /// <param name="source">待检测的 BGR 或灰度图像。</param>
    /// <param name="parameters">检测参数。</param>
    /// <returns>包含标注图、二值图和逐区域数据的检测结果。</returns>
    public InspectionResult Inspect(Mat source, InspectionParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(parameters);
        Validate(source, parameters);

        using Mat colorSource = EnsureBgr(source);
        using Mat roiImage = new(colorSource, parameters.RegionOfInterest);
        Mat binary = ExtractBlackPixels(roiImage, parameters);

        IReadOnlyList<ComponentCandidate> candidates = FindCandidates(binary, parameters);
        MeasurementBatch measurements = MeasureCandidates(binary, roiImage, candidates, parameters);
        List<InspectionRegionResult> regions = measurements.Regions;
        Mat annotated = DrawAnnotations(colorSource, parameters.RegionOfInterest, regions);

        return new InspectionResult
        {
            BinaryRegion = binary,
            AnnotatedImage = annotated,
            EndRegionPreview = measurements.EndRegionPreview,
            Regions = regions
        };
    }

    /// <summary>
    /// 校验图像、ROI 和算法参数，避免 OpenCV 收到非法尺寸。
    /// </summary>
    /// <param name="source">待检测图像。</param>
    /// <param name="parameters">检测参数。</param>
    private static void Validate(Mat source, InspectionParameters parameters)
    {
        if (source.Empty())
        {
            throw new ArgumentException("待检测图像为空。", nameof(source));
        }

        Rect roi = parameters.RegionOfInterest;
        if (roi.Width <= 0 || roi.Height <= 0 || roi.X < 0 || roi.Y < 0 ||
            roi.Right > source.Width || roi.Bottom > source.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters), "右端检测区域超出图像范围。请重新框选。 ");
        }

        if (parameters.MorphologyKernelSize <= 0 || parameters.MorphologyKernelSize % 2 == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters), "闭运算核边长必须为正奇数。 ");
        }

        if (parameters.MinimumComponentArea <= 0 || parameters.MinimumComponentWidth <= 0 ||
            parameters.MinimumComponentHeight <= 0 ||
            parameters.MaximumComponentHeight < parameters.MinimumComponentHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters), "连通域参数不合法。 ");
        }

        if (parameters.TextureSensitivity < 0 || parameters.TextureSensitivity > 100 ||
            parameters.EndBoundaryOffsetPixels < -100 || parameters.EndBoundaryOffsetPixels > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters), "纹理灵敏度或起点修正超出允许范围。 ");
        }
    }

    /// <summary>
    /// 把输入图像统一转换成三通道 BGR 图像，并返回独立副本。
    /// </summary>
    /// <param name="source">原始图像。</param>
    /// <returns>三通道 BGR 图像。</returns>
    private static Mat EnsureBgr(Mat source)
    {
        Mat result = new();
        switch (source.Channels())
        {
            case 1:
                Cv2.CvtColor(source, result, ColorConversionCodes.GRAY2BGR);
                break;
            case 3:
                source.CopyTo(result);
                break;
            case 4:
                Cv2.CvtColor(source, result, ColorConversionCodes.BGRA2BGR);
                break;
            default:
                result.Dispose();
                throw new ArgumentException("仅支持灰度、BGR 或 BGRA 图像。", nameof(source));
        }

        return result;
    }

    /// <summary>
    /// 仅把三个颜色通道都足够暗的像素提取为白色前景，从而排除红色标注框。
    /// </summary>
    /// <param name="roiImage">右端检测区域。</param>
    /// <param name="parameters">检测参数。</param>
    /// <returns>前景为白色的二值图。</returns>
    private static Mat ExtractBlackPixels(Mat roiImage, InspectionParameters parameters)
    {
        Mat[] channels = Cv2.Split(roiImage);
        using Mat maximum01 = new();
        using Mat maximum = new();
        Mat binary = new();

        try
        {
            Cv2.Max(channels[0], channels[1], maximum01);
            Cv2.Max(maximum01, channels[2], maximum);
            Cv2.Threshold(maximum, binary, parameters.BlackThreshold, 255, ThresholdTypes.BinaryInv);

            if (parameters.MorphologyKernelSize > 1)
            {
                using Mat kernel = Cv2.GetStructuringElement(
                    MorphShapes.Rect,
                    new CvSize(parameters.MorphologyKernelSize, parameters.MorphologyKernelSize));
                Cv2.MorphologyEx(binary, binary, MorphTypes.Close, kernel);
            }

            return binary;
        }
        catch
        {
            binary.Dispose();
            throw;
        }
        finally
        {
            foreach (Mat channel in channels)
            {
                channel.Dispose();
            }
        }
    }

    /// <summary>
    /// 提取满足面积与尺寸条件的连通域，并按垂直位置排序。
    /// </summary>
    /// <param name="binary">前景为白色的二值图。</param>
    /// <param name="parameters">检测参数。</param>
    /// <returns>排序后的候选连通域。</returns>
    private static IReadOnlyList<ComponentCandidate> FindCandidates(Mat binary, InspectionParameters parameters)
    {
        using Mat labels = new();
        using Mat stats = new();
        using Mat centroids = new();
        int count = Cv2.ConnectedComponentsWithStats(binary, labels, stats, centroids, PixelConnectivity.Connectivity8);
        List<ComponentCandidate> candidates = new(count);

        for (int label = 1; label < count; label++)
        {
            int x = stats.At<int>(label, (int)ConnectedComponentsTypes.Left);
            int y = stats.At<int>(label, (int)ConnectedComponentsTypes.Top);
            int width = stats.At<int>(label, (int)ConnectedComponentsTypes.Width);
            int height = stats.At<int>(label, (int)ConnectedComponentsTypes.Height);
            int area = stats.At<int>(label, (int)ConnectedComponentsTypes.Area);

            if (area < parameters.MinimumComponentArea || width < parameters.MinimumComponentWidth ||
                height < parameters.MinimumComponentHeight || height > parameters.MaximumComponentHeight)
            {
                continue;
            }

            candidates.Add(new ComponentCandidate(label, new Rect(x, y, width, height), area));
        }

        ComponentCandidate[] ordered = candidates
            .OrderBy(candidate => candidate.Bounds.Y + candidate.Bounds.Height / 2.0)
            .ToArray();
        return FilterByDominantTieGeometry(ordered, binary.Size());
    }

    /// <summary>
    /// 使用同一张图中扎带的主流宽高和填充率排除板缝、金属细线及 ROI 边缘残片。
    /// </summary>
    /// <param name="candidates">完成基础尺寸过滤的候选。</param>
    /// <param name="binarySize">ROI 二值图尺寸。</param>
    /// <returns>保留真实扎带主体后的候选集合。</returns>
    private static IReadOnlyList<ComponentCandidate> FilterByDominantTieGeometry(
        IReadOnlyList<ComponentCandidate> candidates,
        CvSize binarySize)
    {
        if (candidates.Count < 3)
        {
            return candidates;
        }

        double medianWidth = Median(candidates.Select(candidate => (double)candidate.Bounds.Width).ToArray());
        double medianHeight = Median(candidates.Select(candidate => (double)candidate.Bounds.Height).ToArray());
        double minimumWidth = medianWidth * 0.45;
        double maximumWidth = medianWidth * 1.65;
        double minimumHeight = medianHeight * 0.55;
        double maximumHeight = medianHeight * 1.60;

        return candidates
            .Where(candidate =>
            {
                Rect bounds = candidate.Bounds;
                double fillRatio = candidate.Area / (double)(bounds.Width * bounds.Height);
                bool touchesHorizontalRoiEdge = bounds.Top <= 1 || bounds.Bottom >= binarySize.Height - 1;
                return !touchesHorizontalRoiEdge &&
                       bounds.Width >= minimumWidth && bounds.Width <= maximumWidth &&
                       bounds.Height >= minimumHeight && bounds.Height <= maximumHeight &&
                       fillRatio >= 0.35;
            })
            .ToArray();
    }

    /// <summary>
    /// 对候选连通域逐个旋正，先定位局部齿纹边界，再用同图共识补齐并裁剪端头。
    /// </summary>
    /// <param name="binary">前景为白色的二值图。</param>
    /// <param name="roiColor">ROI 对应的彩色图。</param>
    /// <param name="candidates">排序后的候选区域。</param>
    /// <param name="parameters">检测参数。</param>
    /// <returns>逐区域结果和集中端头预览图。</returns>
    private MeasurementBatch MeasureCandidates(
        Mat binary,
        Mat roiColor,
        IReadOnlyList<ComponentCandidate> candidates,
        InspectionParameters parameters)
    {
        List<InspectionRegionResult> results = new(candidates.Count);
        List<PendingRegionMeasurement> pendingMeasurements = new(candidates.Count);
        List<EndRegionTile> previewTiles = new(candidates.Count);
        EndBoundaryLocatorOptions locatorOptions = new(parameters.TextureSensitivity);

        try
        {
            for (int position = 0; position < candidates.Count; position++)
            {
                ComponentCandidate candidate = candidates[position];
                using Mat component = IsolateComponent(binary, candidate.Bounds);
                using Mat colorView = new(roiColor, candidate.Bounds);
                using Mat colorComponent = colorView.Clone();
                AlignedComponent? aligned = null;

                try
                {
                    aligned = AlignHorizontally(component, colorComponent);
                    bool hasLocalBoundary = _boundaryLocator.TryLocate(
                        aligned.Binary,
                        aligned.Color,
                        locatorOptions,
                        out EndBoundaryCandidate localCandidate) &&
                        IsValidBoundaryX(localCandidate.BoundaryX, aligned.Binary.Width);

                    pendingMeasurements.Add(new PendingRegionMeasurement(
                        position + 1,
                        candidate,
                        aligned,
                        hasLocalBoundary ? localCandidate : default,
                        hasLocalBoundary
                            ? EndBoundaryLocationSource.LocalTexture
                            : EndBoundaryLocationSource.None));
                    aligned = null;
                }
                finally
                {
                    aligned?.Dispose();
                }
            }

            CompleteMissingBoundaries(pendingMeasurements, parameters.RegionOfInterest, locatorOptions);

            foreach (PendingRegionMeasurement measurement in pendingMeasurements)
            {
                AlignedComponent aligned = measurement.Aligned;
                bool hasBoundary = measurement.LocationSource != EndBoundaryLocationSource.None &&
                                   IsValidBoundaryX(measurement.BoundaryCandidate.BoundaryX, aligned.Binary.Width);
                int boundaryX = hasBoundary
                    ? Math.Clamp(
                        measurement.BoundaryCandidate.BoundaryX + parameters.EndBoundaryOffsetPixels,
                        2,
                        aligned.Binary.Width - 3)
                    : 0;
                int projectionTop = (int)Math.Floor(aligned.Binary.Height * 0.25);
                int projectionBottom = Math.Max(
                    projectionTop + 1,
                    (int)Math.Ceiling(aligned.Binary.Height * 0.75));
                Rect boundaryBounds = hasBoundary
                    ? new Rect(boundaryX - 1, projectionTop, 1, projectionBottom - projectionTop)
                    : default;
                Rect endBounds = hasBoundary
                    ? new Rect(boundaryX, 0, aligned.Binary.Width - boundaryX, aligned.Binary.Height)
                    : default;

                Mat tileImage;
                if (hasBoundary)
                {
                    using Mat endView = new(aligned.Color, endBounds);
                    tileImage = endView.Clone();
                }
                else
                {
                    tileImage = aligned.Color.Clone();
                }

                previewTiles.Add(new EndRegionTile(measurement.Index, tileImage, hasBoundary));
                Rect imageBounds = new(
                    parameters.RegionOfInterest.X + measurement.Component.Bounds.X,
                    parameters.RegionOfInterest.Y + measurement.Component.Bounds.Y,
                    measurement.Component.Bounds.Width,
                    measurement.Component.Bounds.Height);
                Point2f[] boundaryPolygon = hasBoundary
                    ? aligned.MapRectangleToImage(
                        boundaryBounds,
                        measurement.Component.Bounds,
                        parameters.RegionOfInterest)
                    : Array.Empty<Point2f>();

                results.Add(new InspectionRegionResult
                {
                    Index = measurement.Index,
                    BoundingBox = imageBounds,
                    ComponentArea = measurement.Component.Area,
                    HasEndBoundary = hasBoundary,
                    EndBoundaryBounds = boundaryBounds,
                    BoundaryLocationSource = hasBoundary
                        ? measurement.LocationSource
                        : EndBoundaryLocationSource.None,
                    EndBoundaryPolygon = boundaryPolygon,
                    ExtractedEndBounds = endBounds,
                    AlignedComponentSize = new CvSize(aligned.Binary.Width, aligned.Binary.Height),
                    ExtractedEndPolygon = hasBoundary
                        ? aligned.MapRectangleToImage(
                            endBounds,
                            measurement.Component.Bounds,
                            parameters.RegionOfInterest)
                        : Array.Empty<Point2f>()
                });
            }

            return new MeasurementBatch(results, BuildEndRegionPreview(previewTiles));
        }
        finally
        {
            foreach (EndRegionTile tile in previewTiles)
            {
                tile.Dispose();
            }

            foreach (PendingRegionMeasurement measurement in pendingMeasurements)
            {
                measurement.Dispose();
            }
        }
    }

    /// <summary>
    /// 使用同一张图中已定位的高置信边界补齐局部无纹理区域。
    /// </summary>
    /// <param name="measurements">全部候选区域的第一遍测量。</param>
    /// <param name="regionOfInterest">原图坐标系中的检测 ROI。</param>
    /// <param name="locatorOptions">本次检测统一使用的端头边界定位选项。</param>
    private void CompleteMissingBoundaries(
        IReadOnlyList<PendingRegionMeasurement> measurements,
        Rect regionOfInterest,
        EndBoundaryLocatorOptions locatorOptions)
    {
        List<BoundaryObservation> observations = measurements
            .Where(measurement => measurement.LocationSource == EndBoundaryLocationSource.LocalTexture)
            .Select(measurement =>
            {
                Point2f imagePoint = measurement.Aligned.MapPointToImage(
                    new Point2f(
                        measurement.BoundaryCandidate.BoundaryX,
                        measurement.Aligned.Binary.Height / 2F),
                    measurement.Component.Bounds,
                    regionOfInterest);
                return new BoundaryObservation(imagePoint.Y, imagePoint.X);
            })
            .ToList();

        if (!TryFitBoundaryConsensus(observations, out BoundaryConsensus consensus))
        {
            return;
        }

        foreach (PendingRegionMeasurement measurement in measurements)
        {
            if (measurement.LocationSource != EndBoundaryLocationSource.None)
            {
                continue;
            }

            double imageY = regionOfInterest.Y + measurement.Component.Bounds.Y +
                            measurement.Component.Bounds.Height / 2.0;
            double predictedImageX = consensus.PredictX(imageY);
            Point2f alignedPoint = measurement.Aligned.MapImagePointToAligned(
                new Point2f((float)predictedImageX, (float)imageY),
                measurement.Component.Bounds,
                regionOfInterest);
            int maximumBoundaryX = measurement.Aligned.Binary.Width - Math.Max(
                measurement.Aligned.Binary.Height * 2,
                (int)Math.Round(measurement.Aligned.Binary.Width * 0.25));
            if (maximumBoundaryX < 2)
            {
                continue;
            }

            int predictedBoundaryX = Math.Clamp((int)Math.Round(alignedPoint.X), 2, maximumBoundaryX);
            EndBoundaryCandidate finalCandidate;
            if (_boundaryLocator.TryRefineNear(
                    measurement.Aligned.Binary,
                    measurement.Aligned.Color,
                    predictedBoundaryX,
                    measurement.Aligned.Binary.Height,
                    locatorOptions,
                    out EndBoundaryCandidate refinedCandidate) &&
                Math.Abs(refinedCandidate.BoundaryX - predictedBoundaryX) <= measurement.Aligned.Binary.Height)
            {
                finalCandidate = refinedCandidate;
            }
            else
            {
                finalCandidate = new EndBoundaryCandidate(predictedBoundaryX, 0);
            }

            if (!IsValidBoundaryX(finalCandidate.BoundaryX, measurement.Aligned.Binary.Width))
            {
                continue;
            }

            measurement.BoundaryCandidate = finalCandidate;
            measurement.LocationSource = EndBoundaryLocationSource.ImageConsensus;
        }
    }

    /// <summary>
    /// 使用中位斜率和中位绝对偏差拟合不受少量误选影响的跨行边界。
    /// </summary>
    /// <param name="observations">原图坐标系中的边界观测。</param>
    /// <param name="consensus">成功时返回边界直线。</param>
    /// <returns>至少有三个有效观测并完成拟合时为真。</returns>
    private static bool TryFitBoundaryConsensus(
        IReadOnlyList<BoundaryObservation> observations,
        out BoundaryConsensus consensus)
    {
        consensus = default;
        if (observations.Count < 3)
        {
            return false;
        }

        BoundaryConsensus initial = FitMedianLine(observations);
        double[] residuals = observations
            .Select(observation => Math.Abs(observation.ImageX - initial.PredictX(observation.ImageY)))
            .ToArray();
        double residualMedian = Median(residuals);
        double residualMad = Median(residuals.Select(value => Math.Abs(value - residualMedian)).ToArray());
        double maximumResidual = Math.Max(3.0, residualMedian + 3.0 * Math.Max(1.0, residualMad));
        BoundaryObservation[] inliers = observations
            .Where(observation =>
                Math.Abs(observation.ImageX - initial.PredictX(observation.ImageY)) <= maximumResidual)
            .ToArray();

        consensus = inliers.Length >= 3 ? FitMedianLine(inliers) : initial;
        return true;
    }

    /// <summary>
    /// 使用 Theil-Sen 中位斜率拟合 X 随 Y 变化的直线。
    /// </summary>
    /// <param name="observations">原图边界观测。</param>
    /// <returns>稳健直线参数。</returns>
    private static BoundaryConsensus FitMedianLine(IReadOnlyList<BoundaryObservation> observations)
    {
        List<double> slopes = new(observations.Count * (observations.Count - 1) / 2);
        for (int first = 0; first < observations.Count - 1; first++)
        {
            for (int second = first + 1; second < observations.Count; second++)
            {
                double deltaY = observations[second].ImageY - observations[first].ImageY;
                if (Math.Abs(deltaY) <= double.Epsilon)
                {
                    continue;
                }

                slopes.Add((observations[second].ImageX - observations[first].ImageX) / deltaY);
            }
        }

        double slope = slopes.Count == 0 ? 0 : Median(slopes.ToArray());
        double intercept = Median(
            observations.Select(observation => observation.ImageX - slope * observation.ImageY).ToArray());
        return new BoundaryConsensus(slope, intercept);
    }

    /// <summary>
    /// 判断局部裁剪起点是否能形成至少三像素宽的端头。
    /// </summary>
    /// <param name="boundaryX">局部裁剪起点。</param>
    /// <param name="alignedWidth">旋正组件宽度。</param>
    /// <returns>边界位于组件内部时为真。</returns>
    private static bool IsValidBoundaryX(int boundaryX, int alignedWidth)
    {
        return boundaryX >= 2 && boundaryX <= alignedWidth - 3;
    }

    /// <summary>
    /// 计算一组数值的中位数。
    /// </summary>
    /// <param name="values">待统计数值。</param>
    /// <returns>空数组返回 0，否则返回中位数。</returns>
    private static double Median(double[] values)
    {
        if (values.Length == 0)
        {
            return 0;
        }

        Array.Sort(values);
        int middle = values.Length / 2;
        return values.Length % 2 == 1
            ? values[middle]
            : (values[middle - 1] + values[middle]) / 2.0;
    }

    /// <summary>
    /// 从候选区域复制独立的二值组件图。
    /// 当前候选矩形由单个标签紧密包围，尺寸过滤已排除相邻小噪声。
    /// </summary>
    /// <param name="binary">完整 ROI 二值图。</param>
    /// <param name="bounds">候选区域矩形。</param>
    /// <returns>候选区域的独立二值图。</returns>
    private static Mat IsolateComponent(Mat binary, Rect bounds)
    {
        using Mat view = new(binary, bounds);
        return view.Clone();
    }

    /// <summary>
    /// 根据前景二阶矩估算扎带主轴角度，并使用同一矩阵同步旋正二值图和彩色图。
    /// </summary>
    /// <param name="binaryComponent">紧密包围单个连通域的二值图。</param>
    /// <param name="colorComponent">与二值连通域完全同尺寸的彩色图。</param>
    /// <returns>同步旋正并紧密裁剪的二值图、彩色图和逆变换数据。</returns>
    private static AlignedComponent AlignHorizontally(Mat binaryComponent, Mat colorComponent)
    {
        Moments moments = Cv2.Moments(binaryComponent, true);
        double angleRadians = 0.5 * Math.Atan2(2 * moments.Mu11, moments.Mu20 - moments.Mu02);
        double angleDegrees = angleRadians * 180 / Math.PI;
        int padding = Math.Max(4, binaryComponent.Height);

        using Mat paddedBinary = new();
        Cv2.CopyMakeBorder(
            binaryComponent,
            paddedBinary,
            padding,
            padding,
            padding,
            padding,
            BorderTypes.Constant,
            Scalar.Black);
        using Mat paddedColor = new();
        Cv2.CopyMakeBorder(
            colorComponent,
            paddedColor,
            padding,
            padding,
            padding,
            padding,
            BorderTypes.Constant,
            Scalar.White);
        using Mat rotation = Cv2.GetRotationMatrix2D(
            new Point2f(paddedBinary.Width / 2F, paddedBinary.Height / 2F),
            angleDegrees,
            1.0);
        using Mat rotatedBinary = new();
        Cv2.WarpAffine(
            paddedBinary,
            rotatedBinary,
            rotation,
            paddedBinary.Size(),
            InterpolationFlags.Nearest,
            BorderTypes.Constant,
            Scalar.Black);
        using Mat rotatedColor = new();
        Cv2.WarpAffine(
            paddedColor,
            rotatedColor,
            rotation,
            paddedColor.Size(),
            InterpolationFlags.Linear,
            BorderTypes.Constant,
            Scalar.White);

        using Mat foreground = new();
        Cv2.FindNonZero(rotatedBinary, foreground);
        if (foreground.Empty())
        {
            return AlignedComponent.CreateUnchanged(binaryComponent, colorComponent);
        }

        Rect foregroundBounds = Cv2.BoundingRect(foreground);
        using Mat alignedBinaryView = new(rotatedBinary, foregroundBounds);
        using Mat alignedColorView = new(rotatedColor, foregroundBounds);
        Mat normalizedBinary = alignedBinaryView.Clone();
        Mat normalizedColor = alignedColorView.Clone();
        bool horizontallyFlipped = ShouldFlipToPlaceSmoothTipOnRight(normalizedBinary, normalizedColor);
        if (horizontallyFlipped)
        {
            using Mat flippedBinary = new();
            using Mat flippedColor = new();
            Cv2.Flip(normalizedBinary, flippedBinary, FlipMode.Y);
            Cv2.Flip(normalizedColor, flippedColor, FlipMode.Y);
            flippedBinary.CopyTo(normalizedBinary);
            flippedColor.CopyTo(normalizedColor);
        }

        Mat inverseRotation = new();
        Cv2.InvertAffineTransform(rotation, inverseRotation);
        return new AlignedComponent(
            normalizedBinary,
            normalizedColor,
            rotation.Clone(),
            inverseRotation,
            foregroundBounds,
            padding,
            horizontallyFlipped);
    }

    /// <summary>
    /// 比较组件左右内部区间的列纹理能量，判断是否需要把圆头统一翻转到右侧。
    /// </summary>
    /// <param name="binary">旋正并紧密裁剪后的二值组件。</param>
    /// <param name="color">与二值组件对齐的彩色组件。</param>
    /// <returns>右侧齿纹能量明显高于左侧时为真。</returns>
    private static bool ShouldFlipToPlaceSmoothTipOnRight(Mat binary, Mat color)
    {
        int width = binary.Width;
        int height = binary.Height;
        if (width < 20 || height < 5)
        {
            return false;
        }

        int projectionTop = (int)Math.Floor(height * 0.25);
        int projectionBottom = Math.Max(projectionTop + 1, (int)Math.Ceiling(height * 0.75));
        double[] profile = new double[width];

        for (int x = 0; x < width; x++)
        {
            double sum = 0;
            int count = 0;
            for (int y = projectionTop; y < projectionBottom; y++)
            {
                if (binary.At<byte>(y, x) == 0)
                {
                    continue;
                }

                Vec3b pixel = color.At<Vec3b>(y, x);
                sum += Math.Max(pixel.Item0, Math.Max(pixel.Item1, pixel.Item2));
                count++;
            }

            profile[x] = count == 0 ? (x == 0 ? 0 : profile[x - 1]) : sum / count;
        }

        double[] energy = new double[width];
        for (int x = 1; x < width; x++)
        {
            energy[x] = Math.Abs(profile[x] - profile[x - 1]);
        }

        int leftStart = Math.Max(1, (int)Math.Round(width * 0.05));
        int leftEnd = Math.Max(leftStart + 1, (int)Math.Round(width * 0.45));
        int rightStart = Math.Min(width - 2, (int)Math.Round(width * 0.55));
        int rightEnd = Math.Max(rightStart + 1, (int)Math.Round(width * 0.95));
        double leftEnergy = energy.Skip(leftStart).Take(leftEnd - leftStart).DefaultIfEmpty().Average();
        double rightEnergy = energy.Skip(rightStart).Take(rightEnd - rightStart).DefaultIfEmpty().Average();
        return rightEnergy > leftEnergy * 1.20 && rightEnergy > double.Epsilon;
    }

    /// <summary>
    /// 把逐根彩色端头按序号纵向拼接为一张可缩放预览图。
    /// </summary>
    /// <param name="tiles">端头图块。</param>
    /// <returns>深灰背景的 BGR 预览图。</returns>
    private static Mat BuildEndRegionPreview(IReadOnlyList<EndRegionTile> tiles)
    {
        const int labelWidth = 56;
        const int outerPadding = 8;
        const int rowGap = 8;
        const int minimumRowHeight = 32;

        if (tiles.Count == 0)
        {
            Mat emptyPreview = new(160, 640, MatType.CV_8UC3, new Scalar(38, 41, 46));
            Cv2.PutText(emptyPreview, "NO REGIONS", new CvPoint(24, 88), HersheyFonts.HersheySimplex, 0.8, new Scalar(190, 190, 190), 2);
            return emptyPreview;
        }

        int maximumTileWidth = tiles.Max(tile => tile.Image.Width);
        int totalHeight = outerPadding + tiles.Sum(tile => Math.Max(minimumRowHeight, tile.Image.Height) + rowGap);
        int canvasWidth = outerPadding + labelWidth + maximumTileWidth + outerPadding;
        Mat preview = new(totalHeight, canvasWidth, MatType.CV_8UC3, new Scalar(38, 41, 46));
        int rowTop = outerPadding;

        foreach (EndRegionTile tile in tiles)
        {
            int rowHeight = Math.Max(minimumRowHeight, tile.Image.Height);
            int imageTop = rowTop + (rowHeight - tile.Image.Height) / 2;
            Rect destination = new(outerPadding + labelWidth, imageTop, tile.Image.Width, tile.Image.Height);
            using Mat destinationView = new(preview, destination);
            tile.Image.CopyTo(destinationView);
            Scalar statusColor = tile.HasBoundary ? new Scalar(0, 0, 255) : new Scalar(0, 165, 255);
            Cv2.PutText(
                preview,
                $"#{tile.Index:00}",
                new CvPoint(outerPadding, rowTop + rowHeight / 2 + 6),
                HersheyFonts.HersheySimplex,
                0.55,
                statusColor,
                1,
                LineTypes.AntiAlias);
            Cv2.Line(
                preview,
                new CvPoint(destination.X, imageTop),
                new CvPoint(destination.X, imageTop + tile.Image.Height - 1),
                statusColor,
                2,
                LineTypes.AntiAlias);
            rowTop += rowHeight + rowGap;
        }

        return preview;
    }

    /// <summary>
    /// 在原图副本上绘制 ROI、切割线、区域序号和测量结果。
    /// </summary>
    /// <param name="source">三通道原图。</param>
    /// <param name="roi">检测区域。</param>
    /// <param name="regions">逐区域检测结果。</param>
    /// <returns>标注后的 BGR 图像。</returns>
    private static Mat DrawAnnotations(Mat source, Rect roi, IReadOnlyList<InspectionRegionResult> regions)
    {
        Mat annotated = source.Clone();
        Cv2.Rectangle(annotated, roi, new Scalar(0, 215, 255), 2);
        Cv2.Line(annotated, new CvPoint(roi.X, roi.Y), new CvPoint(roi.X, roi.Bottom), new Scalar(255, 0, 255), 2);

        foreach (InspectionRegionResult region in regions)
        {
            Scalar color = new(0, 200, 0);
            Cv2.Rectangle(annotated, region.BoundingBox, color, 2);
            if (region.HasEndBoundary)
            {
                DrawMappedPolygon(annotated, region.EndBoundaryPolygon, new Scalar(255, 255, 0));
                DrawMappedPolygon(annotated, region.ExtractedEndPolygon, new Scalar(0, 0, 255));
            }

            CvPoint textOrigin = new(region.BoundingBox.X, Math.Max(18, region.BoundingBox.Y - 3));
            Cv2.PutText(
                annotated,
                $"#{region.Index:00}",
                textOrigin,
                HersheyFonts.HersheySimplex,
                0.48,
                color,
                1,
                LineTypes.AntiAlias);
        }

        return annotated;
    }

    /// <summary>
    /// 把浮点原图坐标限制到图像范围后绘制闭合多边形。
    /// </summary>
    /// <param name="image">目标标注图。</param>
    /// <param name="polygon">按顺序排列的浮点顶点。</param>
    /// <param name="color">BGR 线条颜色。</param>
    private static void DrawMappedPolygon(Mat image, IReadOnlyList<Point2f> polygon, Scalar color)
    {
        if (polygon.Count < 3)
        {
            return;
        }

        int imageWidth = image.Width;
        int imageHeight = image.Height;
        CvPoint[] points = polygon
            .Select(point => new CvPoint(
                Math.Clamp((int)Math.Round(point.X), 0, imageWidth - 1),
                Math.Clamp((int)Math.Round(point.Y), 0, imageHeight - 1)))
            .ToArray();
        Cv2.Polylines(image, new[] { points }, true, color, 2, LineTypes.Link8);
    }

    /// <summary>
    /// 保存同步旋正后的二值图、彩色图以及映射回原局部图的逆仿射数据。
    /// </summary>
    private sealed class AlignedComponent : IDisposable
    {
        /// <summary>
        /// 获取旋正并紧密裁剪后的二值连通域。
        /// </summary>
        public Mat Binary { get; }

        /// <summary>
        /// 获取与二值连通域完全对齐的彩色图。
        /// </summary>
        public Mat Color { get; }

        /// <summary>
        /// 保存从填充后原局部图映射到旋转画布的正向仿射矩阵。
        /// </summary>
        private Mat Rotation { get; }

        /// <summary>
        /// 保存从旋转画布坐标映射回填充前局部坐标的逆仿射矩阵。
        /// </summary>
        private Mat InverseRotation { get; }

        /// <summary>
        /// 保存旋转画布中前景紧密矩形。
        /// </summary>
        private Rect ForegroundBounds { get; }

        /// <summary>
        /// 保存旋转前在局部图四周增加的像素边距。
        /// </summary>
        private int Padding { get; }

        /// <summary>
        /// 获取该组件是否为统一圆头方向而执行过水平翻转。
        /// </summary>
        private bool HorizontallyFlipped { get; }

        /// <summary>
        /// 创建同步旋正结果。
        /// </summary>
        /// <param name="binary">旋正二值图。</param>
        /// <param name="color">旋正彩色图。</param>
        /// <param name="rotation">正向仿射矩阵。</param>
        /// <param name="inverseRotation">逆仿射矩阵。</param>
        /// <param name="foregroundBounds">旋转画布中的紧密前景矩形。</param>
        /// <param name="padding">旋转前边距。</param>
        /// <param name="horizontallyFlipped">是否执行过水平翻转。</param>
        public AlignedComponent(
            Mat binary,
            Mat color,
            Mat rotation,
            Mat inverseRotation,
            Rect foregroundBounds,
            int padding,
            bool horizontallyFlipped)
        {
            Binary = binary;
            Color = color;
            Rotation = rotation;
            InverseRotation = inverseRotation;
            ForegroundBounds = foregroundBounds;
            Padding = padding;
            HorizontallyFlipped = horizontallyFlipped;
        }

        /// <summary>
        /// 当前景为空时创建不旋转的安全结果。
        /// </summary>
        /// <param name="binary">原二值局部图。</param>
        /// <param name="color">原彩色局部图。</param>
        /// <returns>使用单位逆变换的结果。</returns>
        public static AlignedComponent CreateUnchanged(Mat binary, Mat color)
        {
            Mat rotation = new(2, 3, MatType.CV_64FC1, Scalar.All(0));
            rotation.Set(0, 0, 1D);
            rotation.Set(1, 1, 1D);
            Mat inverseRotation = rotation.Clone();
            return new AlignedComponent(
                binary.Clone(),
                color.Clone(),
                rotation,
                inverseRotation,
                new Rect(0, 0, binary.Width, binary.Height),
                0,
                false);
        }

        /// <summary>
        /// 把旋正局部矩形的四个角准确映射到原图坐标系。
        /// </summary>
        /// <param name="alignedBounds">旋正局部坐标矩形。</param>
        /// <param name="candidateBounds">ROI 坐标系中的候选连通域矩形。</param>
        /// <param name="roiBounds">原图坐标系中的大 ROI。</param>
        /// <returns>按左上、右上、右下、左下排列的原图浮点坐标。</returns>
        public Point2f[] MapRectangleToImage(Rect alignedBounds, Rect candidateBounds, Rect roiBounds)
        {
            Point2f[] corners =
            {
                new(alignedBounds.Left, alignedBounds.Top),
                new(alignedBounds.Right - 1, alignedBounds.Top),
                new(alignedBounds.Right - 1, alignedBounds.Bottom - 1),
                new(alignedBounds.Left, alignedBounds.Bottom - 1)
            };

            for (int index = 0; index < corners.Length; index++)
            {
                corners[index] = MapPointToImage(corners[index], candidateBounds, roiBounds);
            }

            return corners;
        }

        /// <summary>
        /// 把单个旋正局部点映射到原图坐标系。
        /// </summary>
        /// <param name="point">旋正局部点。</param>
        /// <param name="candidateBounds">ROI 坐标系中的候选矩形。</param>
        /// <param name="roiBounds">原图坐标系中的大 ROI。</param>
        /// <returns>原图坐标点。</returns>
        public Point2f MapPointToImage(Point2f point, Rect candidateBounds, Rect roiBounds)
        {
            double normalizedX = HorizontallyFlipped ? Binary.Width - 1 - point.X : point.X;
            double rotatedX = normalizedX + ForegroundBounds.X;
            double rotatedY = point.Y + ForegroundBounds.Y;
            double paddedX = InverseRotation.At<double>(0, 0) * rotatedX +
                             InverseRotation.At<double>(0, 1) * rotatedY +
                             InverseRotation.At<double>(0, 2);
            double paddedY = InverseRotation.At<double>(1, 0) * rotatedX +
                             InverseRotation.At<double>(1, 1) * rotatedY +
                             InverseRotation.At<double>(1, 2);
            return new Point2f(
                (float)(roiBounds.X + candidateBounds.X + paddedX - Padding),
                (float)(roiBounds.Y + candidateBounds.Y + paddedY - Padding));
        }

        /// <summary>
        /// 把原图坐标点映射到当前旋正组件的局部坐标系。
        /// </summary>
        /// <param name="point">原图坐标点。</param>
        /// <param name="candidateBounds">ROI 坐标系中的候选矩形。</param>
        /// <param name="roiBounds">原图坐标系中的大 ROI。</param>
        /// <returns>旋正组件局部浮点坐标。</returns>
        public Point2f MapImagePointToAligned(Point2f point, Rect candidateBounds, Rect roiBounds)
        {
            double paddedX = point.X - roiBounds.X - candidateBounds.X + Padding;
            double paddedY = point.Y - roiBounds.Y - candidateBounds.Y + Padding;
            double rotatedX = Rotation.At<double>(0, 0) * paddedX +
                              Rotation.At<double>(0, 1) * paddedY +
                              Rotation.At<double>(0, 2);
            double rotatedY = Rotation.At<double>(1, 0) * paddedX +
                              Rotation.At<double>(1, 1) * paddedY +
                              Rotation.At<double>(1, 2);
            return new Point2f(
                (float)(HorizontallyFlipped
                    ? Binary.Width - 1 - (rotatedX - ForegroundBounds.X)
                    : rotatedX - ForegroundBounds.X),
                (float)(rotatedY - ForegroundBounds.Y));
        }

        /// <summary>
        /// 释放旋正结果持有的全部 OpenCV 非托管资源。
        /// </summary>
        public void Dispose()
        {
            Binary.Dispose();
            Color.Dispose();
            Rotation.Dispose();
            InverseRotation.Dispose();
        }
    }

    /// <summary>
    /// 保存第一遍旋正、形状测量和局部边界定位结果，供第二遍同图共识使用。
    /// </summary>
    private sealed class PendingRegionMeasurement : IDisposable
    {
        /// <summary>
        /// 获取从上到下的区域序号。
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// 获取原 ROI 中的连通域候选。
        /// </summary>
        public ComponentCandidate Component { get; }

        /// <summary>
        /// 获取需要跨两遍处理保留的旋正组件。
        /// </summary>
        public AlignedComponent Aligned { get; }

        /// <summary>
        /// 获取或设置最终使用的局部边界候选。
        /// </summary>
        public EndBoundaryCandidate BoundaryCandidate { get; set; }

        /// <summary>
        /// 获取或设置当前边界候选的定位来源。
        /// </summary>
        public EndBoundaryLocationSource LocationSource { get; set; }

        /// <summary>
        /// 创建待完成的区域测量。
        /// </summary>
        /// <param name="index">从上到下的序号。</param>
        /// <param name="component">连通域候选。</param>
        /// <param name="aligned">旋正组件。</param>
        /// <param name="boundaryCandidate">局部边界候选。</param>
        /// <param name="locationSource">边界定位来源。</param>
        public PendingRegionMeasurement(
            int index,
            ComponentCandidate component,
            AlignedComponent aligned,
            EndBoundaryCandidate boundaryCandidate,
            EndBoundaryLocationSource locationSource)
        {
            Index = index;
            Component = component;
            Aligned = aligned;
            BoundaryCandidate = boundaryCandidate;
            LocationSource = locationSource;
        }

        /// <summary>
        /// 释放跨两遍处理保留的旋正图像和仿射矩阵。
        /// </summary>
        public void Dispose()
        {
            Aligned.Dispose();
        }
    }

    /// <summary>
    /// 表示原图坐标系中的一个高置信端头边界观测。
    /// </summary>
    /// <param name="ImageY">边界中心纵坐标。</param>
    /// <param name="ImageX">边界中心横坐标。</param>
    private readonly record struct BoundaryObservation(double ImageY, double ImageX);

    /// <summary>
    /// 表示原图中端头边界横坐标随纵坐标变化的稳健直线。
    /// </summary>
    /// <param name="Slope">X 相对于 Y 的斜率。</param>
    /// <param name="Intercept">X 轴截距。</param>
    private readonly record struct BoundaryConsensus(double Slope, double Intercept)
    {
        /// <summary>
        /// 根据原图纵坐标预测边界横坐标。
        /// </summary>
        /// <param name="imageY">原图纵坐标。</param>
        /// <returns>预测的原图横坐标。</returns>
        public double PredictX(double imageY)
        {
            return Slope * imageY + Intercept;
        }
    }

    /// <summary>
    /// 保存预览拼接前的一根彩色端头图块。
    /// </summary>
    private sealed class EndRegionTile : IDisposable
    {
        /// <summary>
        /// 获取从上到下的区域序号。
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// 获取要粘贴到预览画布的彩色图。
        /// </summary>
        public Mat Image { get; }

        /// <summary>
        /// 获取该图块是否来自成功定位的端头边界。
        /// </summary>
        public bool HasBoundary { get; }

        /// <summary>
        /// 创建端头预览图块。
        /// </summary>
        /// <param name="index">区域序号。</param>
        /// <param name="image">彩色端头图。</param>
        /// <param name="hasBoundary">是否成功定位端头边界。</param>
        public EndRegionTile(int index, Mat image, bool hasBoundary)
        {
            Index = index;
            Image = image;
            HasBoundary = hasBoundary;
        }

        /// <summary>
        /// 释放图块持有的 OpenCV 图像。
        /// </summary>
        public void Dispose()
        {
            Image.Dispose();
        }
    }

    /// <summary>
    /// 保存一次批量测量产生的区域集合和端头预览图。
    /// </summary>
    /// <param name="Regions">逐区域测量结果。</param>
    /// <param name="EndRegionPreview">集中端头预览图。</param>
    private sealed record MeasurementBatch(List<InspectionRegionResult> Regions, Mat EndRegionPreview);

    /// <summary>
    /// 保存一个经过尺寸过滤的连通域标签、矩形和面积。
    /// </summary>
    /// <param name="Label">OpenCV 连通域标签。</param>
    /// <param name="Bounds">ROI 坐标系中的外接矩形。</param>
    /// <param name="Area">连通域像素面积。</param>
    private sealed record ComponentCandidate(int Label, Rect Bounds, int Area);
}
