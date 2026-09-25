using OpenCvSharp;

namespace TieCornerInspection.WinForms.Models;

/// <summary>
/// 表示一个扎带右端连通域的测量结果。
/// </summary>
public sealed class InspectionRegionResult
{
    /// <summary>
    /// 获取或设置从上到下的一维序号。
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// 获取或设置原图坐标系中的连通域外接矩形。
    /// </summary>
    public Rect BoundingBox { get; set; }

    /// <summary>
    /// 获取或设置整个连通域的像素面积。
    /// </summary>
    public int ComponentArea { get; set; }

    /// <summary>
    /// 获取或设置是否成功取得齿纹区结束位置形成的端头边界。
    /// </summary>
    public bool HasEndBoundary { get; set; }

    /// <summary>
    /// 获取或设置端头边界在水平旋正局部图中的一列宽矩形。
    /// 该矩形的右边缘就是最终端头裁剪起点。
    /// </summary>
    public Rect EndBoundaryBounds { get; set; }

    /// <summary>
    /// 获取或设置端头边界的定位来源。
    /// </summary>
    public EndBoundaryLocationSource BoundaryLocationSource { get; set; }

    /// <summary>
    /// 获取或设置映射回原图坐标系的端头边界四边形顶点。
    /// </summary>
    public Point2f[] EndBoundaryPolygon { get; set; } = Array.Empty<Point2f>();

    /// <summary>
    /// 获取或设置最终端头在水平旋正局部图中的裁剪矩形。
    /// </summary>
    public Rect ExtractedEndBounds { get; set; }

    /// <summary>
    /// 获取或设置水平旋正并紧密裁剪后的单根扎带尺寸。
    /// </summary>
    public OpenCvSharp.Size AlignedComponentSize { get; set; }

    /// <summary>
    /// 获取或设置映射回原图坐标系的端头四边形顶点。
    /// </summary>
    public Point2f[] ExtractedEndPolygon { get; set; } = Array.Empty<Point2f>();

}
