using OpenCvSharp;
using TieCornerInspection.WinForms.Models;
using TieCornerInspection.WinForms.Services;
using Xunit;
using Xunit.Abstractions;

namespace TieCornerInspection.Tests;

/// <summary>
/// 使用用户提供的 3072×2048 现场 JPG 验证全部扎带的结构边界和端头裁剪。
/// </summary>
public sealed class SlantedCutSampleInspectionTests
{
    /// <summary>
    /// 保存测试运行时的逐区域诊断输出。
    /// </summary>
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// 创建现场样图端头边界提取回归测试。
    /// </summary>
    /// <param name="output">xUnit 测试输出器。</param>
    public SlantedCutSampleInspectionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// 验证大 ROI 中的 25 根扎带全部从齿纹结束位置裁剪到最右端。
    /// 若实现漏掉任意一根、选错边界或使用固定坐标，本测试会失败。
    /// </summary>
    [Fact]
    public void Inspect_FieldSample_ExtractsEveryEndFromTextureBoundary()
    {
        string imagePath = Path.Combine(AppContext.BaseDirectory, "samples", "扎带右端斜切异常样图.jpg");
        byte[] bytes = File.ReadAllBytes(imagePath);
        using Mat encoded = Mat.FromPixelData(bytes.Length, 1, MatType.CV_8UC1, bytes);
        using Mat image = Cv2.ImDecode(encoded, ImreadModes.Color);
        IRightEndInspector inspector = new OpenCvRightEndInspector();
        InspectionParameters parameters = CreateParameters();

        using InspectionResult result = inspector.Inspect(image, parameters);

        foreach (InspectionRegionResult region in result.Regions)
        {
            _output.WriteLine(
                $"#{region.Index:00} Y={region.BoundingBox.Y,4} " +
                $"边界={(region.HasEndBoundary ? "已找到" : "未找到")} " +
                $"起点X={region.EndBoundaryBounds.Right,4} 来源={region.BoundaryLocationSource} " +
                $"裁剪宽={region.ExtractedEndBounds.Width,4}");
        }

        Assert.Equal(25, result.Regions.Count);
        Assert.All(
            result.Regions,
            region =>
            {
                Assert.True(region.HasEndBoundary, $"第 {region.Index} 根未找到端头边界。");
                Assert.Equal(region.EndBoundaryBounds.Right, region.ExtractedEndBounds.X);
                Assert.Equal(region.AlignedComponentSize.Width, region.ExtractedEndBounds.Right);
                Assert.Equal(region.AlignedComponentSize.Height, region.ExtractedEndBounds.Height);
                Assert.True(region.ExtractedEndBounds.Width >= 3);
                Assert.Equal(4, region.EndBoundaryPolygon.Length);
                Assert.Equal(4, region.ExtractedEndPolygon.Length);
            });
        Assert.All(
            result.Regions,
            region => Assert.InRange(region.ExtractedEndBounds.Width, 150, 220));
        Assert.False(result.EndRegionPreview.Empty());
    }

    /// <summary>
    /// 创建与界面默认值一致的现场图检测参数。
    /// </summary>
    /// <returns>默认检测参数。</returns>
    private static InspectionParameters CreateParameters()
    {
        return new InspectionParameters
        {
            RegionOfInterest = new Rect(1290, 82, 246, 1802),
            BlackThreshold = 180,
            MinimumComponentArea = 120,
            MinimumComponentWidth = 30,
            MinimumComponentHeight = 6,
            MaximumComponentHeight = 60,
            MorphologyKernelSize = 3,
            TextureSensitivity = 60,
            EndBoundaryOffsetPixels = 0
        };
    }
}
