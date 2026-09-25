namespace TieCornerInspection.WinForms.Models;

/// <summary>
/// 保存单根端头结构边界定位所需的可调选项。
/// </summary>
public readonly record struct EndBoundaryLocatorOptions
{
    /// <summary>
    /// 获取纹理灵敏度，范围为 0～100，数值越大越容易保留较弱的末端纹路。
    /// </summary>
    public int TextureSensitivity { get; }

    /// <summary>
    /// 创建端头边界定位选项。
    /// </summary>
    /// <param name="textureSensitivity">纹理灵敏度，范围为 0～100。</param>
    /// <exception cref="ArgumentOutOfRangeException">灵敏度超出 0～100 时抛出。</exception>
    public EndBoundaryLocatorOptions(int textureSensitivity)
    {
        if (textureSensitivity < 0 || textureSensitivity > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(textureSensitivity), "纹理灵敏度必须位于 0～100。 ");
        }

        TextureSensitivity = textureSensitivity;
    }
}
