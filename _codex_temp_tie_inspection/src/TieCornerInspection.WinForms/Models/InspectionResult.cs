using OpenCvSharp;

namespace TieCornerInspection.WinForms.Models;

/// <summary>
/// 封装一次检测产生的图像和逐区域测量结果。
/// </summary>
public sealed class InspectionResult : IDisposable
{
    /// <summary>
    /// 获取二值化后的右端检测区域。
    /// </summary>
    public required Mat BinaryRegion { get; init; }

    /// <summary>
    /// 获取绘制了 ROI、序号、端头边界和截取范围的标注图。
    /// </summary>
    public required Mat AnnotatedImage { get; init; }

    /// <summary>
    /// 获取按扎带序号纵向排列的彩色端头提取预览图。
    /// </summary>
    public required Mat EndRegionPreview { get; init; }

    /// <summary>
    /// 获取从上到下排序的区域结果。
    /// </summary>
    public required IReadOnlyList<InspectionRegionResult> Regions { get; init; }

    /// <summary>
    /// 释放本次结果持有的 OpenCV 非托管图像资源。
    /// </summary>
    public void Dispose()
    {
        BinaryRegion.Dispose();
        AnnotatedImage.Dispose();
        EndRegionPreview.Dispose();
    }
}
