using OpenCvSharp;
using TieCornerInspection.WinForms.Models;
using TieCornerInspection.WinForms.Services;
using Xunit;

namespace TieCornerInspection.Tests;

/// <summary>
/// 验证同一张图内的高置信纹理边界可以稳健补齐局部无纹理扎带。
/// </summary>
public sealed class EndBoundaryConsensusTests
{
    /// <summary>
    /// 圆头朝左、齿纹朝右的扎带应自动归一化方向，并仍裁出圆头一侧。
    /// 缺少方向归一化或坐标还原会使本测试失败。
    /// </summary>
    [Fact]
    public void Inspect_LeftFacingRows_NormalizesDirectionBeforeBoundaryDetection()
    {
        using Mat image = CreateLeftFacingRowImage(5, 92);
        using InspectionResult result = new OpenCvRightEndInspector().Inspect(image, CreateParameters(image));

        Assert.Equal(5, result.Regions.Count);
        Assert.All(
            result.Regions,
            region =>
            {
                Assert.True(region.HasEndBoundary);
                Assert.InRange(region.ExtractedEndBounds.Width, 88, 98);
                Assert.Equal(region.AlignedComponentSize.Width, region.ExtractedEndBounds.Right);
            });
    }

    /// <summary>
    /// 五行中的中间一行完全没有局部纹理时，应由其他四行的共同边界补齐。
    /// 删除第二遍共识处理会使本测试失败。
    /// </summary>
    [Fact]
    public void Inspect_OneSmoothRow_UsesOtherRowsToCompleteBoundary()
    {
        const int expectedBoundaryX = 82;
        using Mat image = CreateRowImage(5, expectedBoundaryX, 2, -1);
        using InspectionResult result = new OpenCvRightEndInspector().Inspect(image, CreateParameters(image));

        Assert.Equal(5, result.Regions.Count);
        InspectionRegionResult completed = result.Regions[2];
        Assert.True(completed.HasEndBoundary);
        Assert.Equal(EndBoundaryLocationSource.ImageConsensus, completed.BoundaryLocationSource);
        Assert.InRange(completed.EndBoundaryBounds.Right, expectedBoundaryX - 3, expectedBoundaryX + 3);
    }

    /// <summary>
    /// 一个明显偏移的局部结果不能拉偏另外一根无纹理扎带的共识预测。
    /// 使用普通平均值而不做稳健离群排除会使本测试失败。
    /// </summary>
    [Fact]
    public void Inspect_OneOutlierRow_DoesNotPullConsensusAwayFromTrueBoundary()
    {
        const int expectedBoundaryX = 84;
        using Mat image = CreateRowImage(7, expectedBoundaryX, 5, 3);
        using InspectionResult result = new OpenCvRightEndInspector().Inspect(image, CreateParameters(image));

        Assert.Equal(7, result.Regions.Count);
        InspectionRegionResult completed = result.Regions[5];
        Assert.True(completed.HasEndBoundary);
        Assert.Equal(EndBoundaryLocationSource.ImageConsensus, completed.BoundaryLocationSource);
        Assert.InRange(completed.EndBoundaryBounds.Right, expectedBoundaryX - 3, expectedBoundaryX + 3);
    }

    /// <summary>
    /// 创建多行黑色扎带图，其中指定行可设为无纹理或错误偏移纹理。
    /// </summary>
    /// <param name="rowCount">扎带行数。</param>
    /// <param name="normalBoundaryX">正常行相对组件左边缘的边界位置。</param>
    /// <param name="smoothRowIndex">完全无纹理的零基行号。</param>
    /// <param name="outlierRowIndex">错误偏移纹理的零基行号，负数表示不创建。</param>
    /// <returns>白底多行 BGR 图。</returns>
    private static Mat CreateRowImage(
        int rowCount,
        int normalBoundaryX,
        int smoothRowIndex,
        int outlierRowIndex)
    {
        const int componentLeft = 12;
        const int componentWidth = 190;
        const int componentHeight = 24;
        const int rowGap = 20;
        Mat image = new(
            20 + rowCount * (componentHeight + rowGap),
            230,
            MatType.CV_8UC3,
            Scalar.White);

        for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            int top = 10 + rowIndex * (componentHeight + rowGap);
            Rect body = new(componentLeft, top, componentWidth - componentHeight / 2, componentHeight);
            Cv2.Rectangle(image, body, new Scalar(70, 70, 70), -1);
            Cv2.Ellipse(
                image,
                new Point(componentLeft + componentWidth - componentHeight / 2, top + componentHeight / 2),
                new OpenCvSharp.Size(componentHeight / 2, componentHeight / 2),
                0,
                -90,
                90,
                new Scalar(70, 70, 70),
                -1);

            if (rowIndex == smoothRowIndex)
            {
                continue;
            }

            int rowBoundaryX = rowIndex == outlierRowIndex ? normalBoundaryX + 45 : normalBoundaryX;
            for (int localX = 0; localX < rowBoundaryX; localX++)
            {
                int value = localX % 6 < 3 ? 125 : 45;
                int imageX = componentLeft + localX;
                Cv2.Line(
                    image,
                    new Point(imageX, top),
                    new Point(imageX, top + componentHeight - 1),
                    new Scalar(value, value, value));
            }
        }

        return image;
    }

    /// <summary>
    /// 创建圆头朝左、齿纹朝右的多行扎带图。
    /// </summary>
    /// <param name="rowCount">扎带行数。</param>
    /// <param name="boundaryX">原始局部坐标中的光滑端头右边界。</param>
    /// <returns>白底多行 BGR 图。</returns>
    private static Mat CreateLeftFacingRowImage(int rowCount, int boundaryX)
    {
        const int componentLeft = 12;
        const int componentWidth = 190;
        const int componentHeight = 24;
        const int rowGap = 20;
        Mat image = new(
            20 + rowCount * (componentHeight + rowGap),
            230,
            MatType.CV_8UC3,
            Scalar.White);

        for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            int top = 10 + rowIndex * (componentHeight + rowGap);
            Cv2.Rectangle(
                image,
                new Rect(
                    componentLeft + componentHeight / 2,
                    top,
                    componentWidth - componentHeight / 2,
                    componentHeight),
                new Scalar(70, 70, 70),
                -1);
            Cv2.Ellipse(
                image,
                new Point(componentLeft + componentHeight / 2, top + componentHeight / 2),
                new OpenCvSharp.Size(componentHeight / 2, componentHeight / 2),
                0,
                90,
                270,
                new Scalar(70, 70, 70),
                -1);

            for (int localX = boundaryX; localX < componentWidth; localX++)
            {
                int value = localX % 6 < 3 ? 125 : 45;
                int imageX = componentLeft + localX;
                Cv2.Line(
                    image,
                    new Point(imageX, top),
                    new Point(imageX, top + componentHeight - 1),
                    new Scalar(value, value, value));
            }
        }

        return image;
    }

    /// <summary>
    /// 创建覆盖整张合成图的检测参数。
    /// </summary>
    /// <param name="image">合成图。</param>
    /// <returns>适合合成扎带尺寸的参数。</returns>
    private static InspectionParameters CreateParameters(Mat image)
    {
        return new InspectionParameters
        {
            RegionOfInterest = new Rect(0, 0, image.Width, image.Height),
            BlackThreshold = 180,
            MinimumComponentArea = 200,
            MinimumComponentWidth = 80,
            MinimumComponentHeight = 10,
            MaximumComponentHeight = 40,
            MorphologyKernelSize = 1,
            TextureSensitivity = 60,
            EndBoundaryOffsetPixels = 0
        };
    }
}
