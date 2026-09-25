using OpenCvSharp;

namespace TieCornerInspection.WinForms.Models;

/// <summary>
/// 定义右端缺角检测使用的全部可调参数。
/// </summary>
public sealed class InspectionParameters
{
    /// <summary>
    /// 获取或设置原图坐标系中的右端检测区域。
    /// </summary>
    public Rect RegionOfInterest { get; set; }

    /// <summary>
    /// 获取或设置黑色像素的通道最大值阈值。
    /// </summary>
    public byte BlackThreshold { get; set; } = 180;

    /// <summary>
    /// 获取或设置保留连通域的最小面积。
    /// </summary>
    public int MinimumComponentArea { get; set; } = 120;

    /// <summary>
    /// 获取或设置保留连通域的最小宽度。
    /// </summary>
    public int MinimumComponentWidth { get; set; } = 30;

    /// <summary>
    /// 获取或设置保留连通域的最小高度。
    /// </summary>
    public int MinimumComponentHeight { get; set; } = 6;

    /// <summary>
    /// 获取或设置保留连通域的最大高度。
    /// </summary>
    public int MaximumComponentHeight { get; set; } = 60;

    /// <summary>
    /// 获取或设置端头纹理灵敏度，范围为 0～100，数值越大越容易保留较弱纹路。
    /// </summary>
    public int TextureSensitivity { get; set; } = 60;

    /// <summary>
    /// 获取或设置自动定位后的端头起点像素修正，正数向圆头方向移动。
    /// </summary>
    public int EndBoundaryOffsetPixels { get; set; }

    /// <summary>
    /// 获取或设置形态学闭运算核边长，必须为正奇数。
    /// </summary>
    public int MorphologyKernelSize { get; set; } = 3;

}
