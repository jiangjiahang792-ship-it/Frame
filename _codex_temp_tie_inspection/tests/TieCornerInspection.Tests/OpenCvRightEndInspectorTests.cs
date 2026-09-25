using OpenCvSharp;
using TieCornerInspection.WinForms.Models;
using TieCornerInspection.WinForms.Services;
using Xunit;

namespace TieCornerInspection.Tests;

/// <summary>
/// 验证 OpenCV 右端检测器的核心可观察行为。
/// </summary>
public sealed class OpenCvRightEndInspectorTests
{
    /// <summary>
    /// 起点修正必须在自动定位后沿端头方向按像素精确移动。
    /// 若偏移未应用或方向写反，本测试会失败。
    /// </summary>
    /// <param name="offsetPixels">期望应用的正负像素偏移。</param>
    [Theory]
    [InlineData(8)]
    [InlineData(-6)]
    public void Inspect_EndBoundaryOffset_MovesCropStartByRequestedPixels(int offsetPixels)
    {
        using Mat image = CreateWhiteImage(260, 90);
        DrawTie(image, 20, 45, 220, hasMissingRoundTip: false);
        DrawLowBrightnessTexture(image, 20, 140, 45);
        InspectionParameters baselineParameters = CreateSyntheticParameters(image);
        InspectionParameters adjustedParameters = CreateSyntheticParameters(image);
        adjustedParameters.EndBoundaryOffsetPixels = offsetPixels;

        using InspectionResult baseline = new OpenCvRightEndInspector().Inspect(image, baselineParameters);
        using InspectionResult adjusted = new OpenCvRightEndInspector().Inspect(image, adjustedParameters);

        InspectionRegionResult baselineRegion = Assert.Single(baseline.Regions);
        InspectionRegionResult adjustedRegion = Assert.Single(adjusted.Regions);
        Assert.Equal(
            baselineRegion.ExtractedEndBounds.X + offsetPixels,
            adjustedRegion.ExtractedEndBounds.X);
    }

    /// <summary>
    /// 过大的正向修正必须被限制，确保端头仍至少保留三像素宽度。
    /// </summary>
    [Fact]
    public void Inspect_ExcessivePositiveOffset_StillKeepsMinimumEndWidth()
    {
        using Mat image = CreateWhiteImage(260, 90);
        DrawTie(image, 20, 45, 220, hasMissingRoundTip: false);
        DrawLowBrightnessTexture(image, 20, 140, 45);
        InspectionParameters parameters = CreateSyntheticParameters(image);
        parameters.EndBoundaryOffsetPixels = 100;

        using InspectionResult result = new OpenCvRightEndInspector().Inspect(image, parameters);

        InspectionRegionResult region = Assert.Single(result.Regions);
        Assert.Equal(3, region.ExtractedEndBounds.Width);
    }

    /// <summary>
    /// 验证输出严格按图像中的垂直位置从上到下排序。
    /// 若实现依赖连通域原始标签顺序，本测试应捕获排序回归。
    /// </summary>
    [Fact]
    public void Inspect_MultipleRows_ReturnsRegionsOrderedFromTopToBottom()
    {
        using Mat image = CreateWhiteImage(280, 170);
        DrawTie(image, 30, 125, 230, hasMissingRoundTip: true);
        DrawTie(image, 30, 30, 230, hasMissingRoundTip: false);
        DrawTie(image, 30, 78, 230, hasMissingRoundTip: false);

        IRightEndInspector inspector = new OpenCvRightEndInspector();
        InspectionParameters parameters = CreateSyntheticParameters(image);

        using InspectionResult result = inspector.Inspect(image, parameters);

        Assert.Equal(3, result.Regions.Count);
        Assert.True(result.Regions[0].BoundingBox.Y < result.Regions[1].BoundingBox.Y);
        Assert.True(result.Regions[1].BoundingBox.Y < result.Regions[2].BoundingBox.Y);
        Assert.Equal(3, result.Regions[2].Index);
    }

    /// <summary>
    /// 验证彩色端头从结构边界右边缘连续裁剪到扎带最右端，并保留完整高度。
    /// 若实现从固定坐标或圆头局部开始裁剪，本测试会失败。
    /// </summary>
    [Fact]
    public void Inspect_TexturedTie_ExtractsFromBoundaryToTip()
    {
        using Mat image = CreateWhiteImage(260, 90);
        DrawTie(image, 20, 45, 220, hasMissingRoundTip: false);
        DrawLowBrightnessTexture(image, 20, 140, 45);

        using InspectionResult result = new OpenCvRightEndInspector().Inspect(
            image,
            CreateSyntheticParameters(image));
        InspectionRegionResult region = Assert.Single(result.Regions);

        Assert.True(region.HasEndBoundary);
        Assert.Equal(region.EndBoundaryBounds.Right, region.ExtractedEndBounds.X);
        Assert.Equal(region.AlignedComponentSize.Width, region.ExtractedEndBounds.Right);
        Assert.Equal(region.AlignedComponentSize.Height, region.ExtractedEndBounds.Height);
        Assert.True(region.ExtractedEndBounds.Width >= 3);
        Assert.False(result.EndRegionPreview.Empty());
        using Mat cyanMask = new();
        Cv2.InRange(result.AnnotatedImage, new Scalar(255, 255, 0), new Scalar(255, 255, 0), cyanMask);
        Assert.True(Cv2.CountNonZero(cyanMask) > 0);
    }

    /// <summary>
    /// 创建用于测试的纯白三通道图像。
    /// </summary>
    /// <param name="width">图像宽度。</param>
    /// <param name="height">图像高度。</param>
    /// <returns>白色 BGR 图像。</returns>
    private static Mat CreateWhiteImage(int width, int height)
    {
        return new Mat(new Size(width, height), MatType.CV_8UC3, Scalar.White);
    }

    /// <summary>
    /// 绘制一根黑色圆头扎带，并可移除右侧半圆形成与现场样图一致的平切端。
    /// </summary>
    /// <param name="image">目标图像。</param>
    /// <param name="left">扎带左端横坐标。</param>
    /// <param name="centerY">扎带中心纵坐标。</param>
    /// <param name="right">扎带右端横坐标。</param>
    /// <param name="hasMissingRoundTip">是否移除右侧圆头。</param>
    private static void DrawTie(Mat image, int left, int centerY, int right, bool hasMissingRoundTip)
    {
        Cv2.Line(image, new Point(left, centerY), new Point(right - 10, centerY), Scalar.Black, 20, LineTypes.AntiAlias);
        Cv2.Circle(image, new Point(right - 10, centerY), 10, Scalar.Black, -1, LineTypes.AntiAlias);

        if (hasMissingRoundTip)
        {
            Cv2.Rectangle(image, new Rect(right - 10, centerY - 12, 14, 24), Scalar.White, -1);
        }
    }

    /// <summary>
    /// 在黑色扎带内部绘制仍低于二值阈值的周期灰纹，保证连通域不被打断。
    /// </summary>
    /// <param name="image">目标彩色图。</param>
    /// <param name="left">纹理起始横坐标。</param>
    /// <param name="right">纹理结束横坐标，不包含该位置。</param>
    /// <param name="centerY">扎带中心纵坐标。</param>
    private static void DrawLowBrightnessTexture(Mat image, int left, int right, int centerY)
    {
        for (int x = left; x < right; x += 6)
        {
            Cv2.Rectangle(
                image,
                new Rect(x, centerY - 7, 3, 15),
                new Scalar(70, 70, 70),
                -1);
        }
    }

    /// <summary>
    /// 创建与合成图尺寸相符的检测参数。
    /// </summary>
    /// <param name="image">合成测试图。</param>
    /// <returns>测试参数。</returns>
    private static InspectionParameters CreateSyntheticParameters(Mat image)
    {
        return new InspectionParameters
        {
            RegionOfInterest = new Rect(0, 0, image.Width, image.Height),
            BlackThreshold = 100,
            MinimumComponentArea = 100,
            MinimumComponentWidth = 80,
            MinimumComponentHeight = 10,
            MaximumComponentHeight = 35,
            MorphologyKernelSize = 1,
            TextureSensitivity = 60,
            EndBoundaryOffsetPixels = 0
        };
    }
}
