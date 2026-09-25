namespace TieCornerInspection.WinForms.Models;

/// <summary>
/// 指示最终端头边界由哪一层定位得到。
/// </summary>
public enum EndBoundaryLocationSource
{
    /// <summary>
    /// 当前区域最终没有得到合法端头边界。
    /// </summary>
    None,

    /// <summary>
    /// 由当前单根扎带自身的齿纹转折直接定位。
    /// </summary>
    LocalTexture,

    /// <summary>
    /// 由同一张图其他高置信扎带形成的整体边界补齐。
    /// </summary>
    ImageConsensus
}
