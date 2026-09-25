using OpenCvSharp;
using TieCornerInspection.WinForms.Models;

namespace TieCornerInspection.WinForms.Services;

/// <summary>
/// 定义单根旋正扎带的齿纹结束位置定位策略，便于替换不同结构算法。
/// </summary>
public interface IRightEndBoundaryLocator
{
    /// <summary>
    /// 在完整局部组件中定位齿纹区到光滑端头的最后转折。
    /// </summary>
    /// <param name="alignedBinaryComponent">旋正后前景为 255 的单通道组件掩膜。</param>
    /// <param name="alignedColorComponent">与掩膜使用相同几何变换的三通道彩色组件。</param>
    /// <param name="options">端头结构边界定位选项。</param>
    /// <param name="candidate">成功时返回裁剪起点和结构置信度。</param>
    /// <returns>存在足够单根纹理证据时为真。</returns>
    bool TryLocate(
        Mat alignedBinaryComponent,
        Mat alignedColorComponent,
        EndBoundaryLocatorOptions options,
        out EndBoundaryCandidate candidate);

    /// <summary>
    /// 在同图共识给出的预测位置附近细化到最近的真实结构转折。
    /// </summary>
    /// <param name="alignedBinaryComponent">旋正后前景为 255 的单通道组件掩膜。</param>
    /// <param name="alignedColorComponent">与掩膜使用相同几何变换的三通道彩色组件。</param>
    /// <param name="predictedBoundaryX">同图共识预测的局部裁剪起点。</param>
    /// <param name="searchRadius">预测位置左右允许搜索的列数。</param>
    /// <param name="options">端头结构边界定位选项。</param>
    /// <param name="candidate">成功时返回细化后的边界候选。</param>
    /// <returns>预测窗口内存在可靠转折时为真。</returns>
    bool TryRefineNear(
        Mat alignedBinaryComponent,
        Mat alignedColorComponent,
        int predictedBoundaryX,
        int searchRadius,
        EndBoundaryLocatorOptions options,
        out EndBoundaryCandidate candidate);
}
