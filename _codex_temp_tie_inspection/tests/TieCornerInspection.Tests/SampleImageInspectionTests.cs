using OpenCvSharp;
using TieCornerInspection.WinForms.Models;
using TieCornerInspection.WinForms.Services;
using Xunit;
using Xunit.Abstractions;

namespace TieCornerInspection.Tests;

/// <summary>
/// 使用用户提供的现场样图验证黑色区域提取与端头截取输出。
/// </summary>
public sealed class SampleImageInspectionTests
{
    /// <summary>
    /// 保存测试运行时的逐区域文字输出。
    /// </summary>
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// 创建样图集成测试。
    /// </summary>
    /// <param name="output">xUnit 测试输出器。</param>
    public SampleImageInspectionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// 验证样图能分割出稳定的多行扎带，并为每个区域输出合法端头截取范围。
    /// 若 ROI、黑色提取或尺寸过滤退化，本测试应失败。
    /// </summary>
    [Fact]
    public void Inspect_ProvidedSample_ReportsEveryExtractedEndRegion()
    {
        string imagePath = Path.Combine(AppContext.BaseDirectory, "samples", "扎带右端样图.png");
        byte[] bytes = File.ReadAllBytes(imagePath);
        using Mat encoded = Mat.FromPixelData(bytes.Length, 1, MatType.CV_8UC1, bytes);
        using Mat image = Cv2.ImDecode(encoded, ImreadModes.Color);
        IRightEndInspector inspector = new OpenCvRightEndInspector();
        InspectionParameters parameters = new()
        {
            RegionOfInterest = new Rect(810, 50, 147, 1125),
            BlackThreshold = 180,
            MinimumComponentArea = 120,
            MinimumComponentWidth = 30,
            MinimumComponentHeight = 6,
            MaximumComponentHeight = 40,
            MorphologyKernelSize = 3,
            TextureSensitivity = 60,
            EndBoundaryOffsetPixels = 0
        };

        using InspectionResult result = inspector.Inspect(image, parameters);

        foreach (InspectionRegionResult region in result.Regions)
        {
            _output.WriteLine(
                $"#{region.Index:00} Y={region.BoundingBox.Y,4} " +
                $"起点X={region.EndBoundaryBounds.Right,4} " +
                $"裁剪宽={region.ExtractedEndBounds.Width,4} 来源={region.BoundaryLocationSource}");
        }

        Assert.InRange(result.Regions.Count, 25, 40);
        Assert.All(result.Regions, region => Assert.True(region.HasEndBoundary));
        Assert.All(result.Regions, region => Assert.True(region.ExtractedEndBounds.Width >= 3));
    }
}
