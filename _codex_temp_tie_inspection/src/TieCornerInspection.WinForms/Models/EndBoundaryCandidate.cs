namespace TieCornerInspection.WinForms.Models;

/// <summary>
/// 表示单根扎带局部纹理定位得到的端头边界候选。
/// </summary>
/// <param name="BoundaryX">旋正局部坐标中的端头裁剪起始列。</param>
/// <param name="Confidence">纹理区与光滑区分离程度形成的非负置信度。</param>
public readonly record struct EndBoundaryCandidate(int BoundaryX, double Confidence);
