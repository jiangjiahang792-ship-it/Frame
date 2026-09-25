using OpenCvSharp;
using TieCornerInspection.WinForms.Models;

namespace TieCornerInspection.WinForms.Services;

/// <summary>
/// 定义可替换的扎带右端检测器契约。
/// </summary>
public interface IRightEndInspector
{
    /// <summary>
    /// 在指定图像和参数上执行黑色连通域分割与端头区域截取。
    /// </summary>
    /// <param name="source">待检测的 BGR 或灰度图像。</param>
    /// <param name="parameters">检测参数。</param>
    /// <returns>包含标注图、二值图和逐区域数据的检测结果。</returns>
    InspectionResult Inspect(Mat source, InspectionParameters parameters);
}
