using OpenCvSharp;
using TieCornerInspection.WinForms.Models;
using TieCornerInspection.WinForms.Services;
using Xunit;
using Xunit.Abstractions;

namespace TieCornerInspection.Tests;

/// <summary>
/// 使用用户提供的 NG 文件夹验证每个有效扎带都能取得端头起点。
/// </summary>
public sealed class NgFolderBatchInspectionTests
{
    /// <summary>
    /// 用户提供的完整 NG 现场图目录。
    /// </summary>
    private const string NgFolderPath = @"C:\Users\34652\Desktop\20260728\20260728\NG";

    /// <summary>
    /// 保存逐图片和逐区域诊断信息。
    /// </summary>
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// 创建 NG 文件夹批量回归测试。
    /// </summary>
    /// <param name="output">xUnit 测试输出器。</param>
    public NgFolderBatchInspectionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// 当前结构边界定位器必须在全部 NG 图片的每个有效区域内给出裁剪起点。
    /// 该测试在修复前用于稳定暴露具体漏检图片和序号。
    /// </summary>
    [Fact]
    public void Inspect_NgFolder_ExtractsEveryEndBoundary()
    {
        string[] imagePaths = Directory.GetFiles(NgFolderPath, "*.jpg")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.Equal(45, imagePaths.Length);

        List<string> failures = new();
        int totalRegions = 0;
        int totalLocal = 0;
        int totalConsensus = 0;

        foreach (string imagePath in imagePaths)
        {
            using Mat image = LoadColorImage(imagePath);
            using InspectionResult result = new OpenCvRightEndInspector().Inspect(image, CreateFieldParameters());
            int[] missingIndexes = result.Regions
                .Where(region => !region.HasEndBoundary)
                .Select(region => region.Index)
                .ToArray();
            int localCount = result.Regions.Count(
                region => region.BoundaryLocationSource == EndBoundaryLocationSource.LocalTexture);
            int consensusCount = result.Regions.Count(
                region => region.BoundaryLocationSource == EndBoundaryLocationSource.ImageConsensus);
            totalRegions += result.Regions.Count;
            totalLocal += localCount;
            totalConsensus += consensusCount;
            int minimumWidth = result.Regions.Min(region => region.ExtractedEndBounds.Width);
            int maximumWidth = result.Regions.Max(region => region.ExtractedEndBounds.Width);

            _output.WriteLine(
                $"{Path.GetFileName(imagePath)} 区域={result.Regions.Count} " +
                $"局部={localCount} 补齐={consensusCount} " +
                $"宽度={minimumWidth}～{maximumWidth} 未找到=[{string.Join(',', missingIndexes)}]");

            if (missingIndexes.Length > 0)
            {
                failures.Add($"{Path.GetFileName(imagePath)}:[{string.Join(',', missingIndexes)}]");
            }

            Assert.All(
                result.Regions,
                region =>
                {
                    Assert.True(region.HasEndBoundary);
                    Assert.NotEqual(EndBoundaryLocationSource.None, region.BoundaryLocationSource);
                    Assert.Equal(region.EndBoundaryBounds.Right, region.ExtractedEndBounds.X);
                    Assert.Equal(region.AlignedComponentSize.Width, region.ExtractedEndBounds.Right);
                    Assert.Equal(region.AlignedComponentSize.Height, region.ExtractedEndBounds.Height);
                    if (region.ExtractedEndBounds.Width < 150 || region.ExtractedEndBounds.Width > 260)
                    {
                        _output.WriteLine(
                            $"  异常 #{region.Index:00} 框={region.BoundingBox} " +
                            $"旋正={region.AlignedComponentSize} 裁剪宽={region.ExtractedEndBounds.Width} " +
                            $"来源={region.BoundaryLocationSource}");
                    }

                    Assert.InRange(region.ExtractedEndBounds.Width, 150, 260);
                    Assert.Equal(4, region.EndBoundaryPolygon.Length);
                    Assert.Equal(4, region.ExtractedEndPolygon.Length);
                });
        }

        _output.WriteLine(
            $"合计区域={totalRegions} 局部={totalLocal} 补齐={totalConsensus} " +
            $"未找到={totalRegions - totalLocal - totalConsensus}");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// 使用字节方式读取中文路径 JPG，避免 OpenCV 原生路径兼容差异。
    /// </summary>
    /// <param name="imagePath">完整图片路径。</param>
    /// <returns>解码后的三通道彩色图。</returns>
    private static Mat LoadColorImage(string imagePath)
    {
        byte[] bytes = File.ReadAllBytes(imagePath);
        using Mat encoded = Mat.FromPixelData(bytes.Length, 1, MatType.CV_8UC1, bytes);
        Mat image = Cv2.ImDecode(encoded, ImreadModes.Color);
        Assert.False(image.Empty(), $"图片解码失败：{imagePath}");
        return image;
    }

    /// <summary>
    /// 创建与 3072×2048 图片界面默认值一致的检测参数。
    /// </summary>
    /// <returns>现场图检测参数。</returns>
    private static InspectionParameters CreateFieldParameters()
    {
        return new InspectionParameters
        {
            RegionOfInterest = new Rect(1290, 82, 492, 1802),
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
