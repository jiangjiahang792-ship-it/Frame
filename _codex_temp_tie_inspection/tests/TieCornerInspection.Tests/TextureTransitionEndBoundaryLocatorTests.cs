using OpenCvSharp;
using TieCornerInspection.WinForms.Models;
using TieCornerInspection.WinForms.Services;
using Xunit;

namespace TieCornerInspection.Tests;

/// <summary>
/// 验证齿纹到光滑端头的结构边界不依赖整图绝对亮度。
/// </summary>
public sealed class TextureTransitionEndBoundaryLocatorTests
{
    /// <summary>
    /// 最强纹理转折右侧仍存在较弱纹路时，高灵敏度必须选择最后一条弱纹右侧。
    /// 若实现继续选择全局最高置信转折，本测试会落在约 X=120 而失败。
    /// </summary>
    [Fact]
    public void TryLocate_WeakTextureAfterStrongTransition_ReturnsRightmostCredibleBoundary()
    {
        using Mat binary = CreateSolidTieMask(240, 24);
        using Mat color = CreateStrongThenWeakTextureTie(240, 24);
        IRightEndBoundaryLocator locator = new TextureTransitionEndBoundaryLocator();

        bool found = locator.TryLocate(
            binary,
            color,
            new EndBoundaryLocatorOptions(80),
            out EndBoundaryCandidate candidate);

        Assert.True(found);
        Assert.InRange(candidate.BoundaryX, 152, 158);
    }

    /// <summary>
    /// 提高纹理灵敏度后，截取起点不得比低灵敏度更靠左。
    /// 若灵敏度映射方向写反，本测试会失败。
    /// </summary>
    [Fact]
    public void TryLocate_HigherSensitivity_DetectsAtLeastAsFarRightAsLowSensitivity()
    {
        using Mat binary = CreateSolidTieMask(240, 24);
        using Mat color = CreateStrongThenWeakTextureTie(240, 24);
        IRightEndBoundaryLocator locator = new TextureTransitionEndBoundaryLocator();

        bool lowFound = locator.TryLocate(
            binary,
            color,
            new EndBoundaryLocatorOptions(20),
            out EndBoundaryCandidate lowCandidate);
        bool highFound = locator.TryLocate(
            binary,
            color,
            new EndBoundaryLocatorOptions(80),
            out EndBoundaryCandidate highCandidate);

        Assert.True(lowFound);
        Assert.True(highFound);
        Assert.True(highCandidate.BoundaryX >= lowCandidate.BoundaryX);
        Assert.InRange(highCandidate.BoundaryX, 152, 158);
    }

    /// <summary>
    /// 整体亮度改变时，结构边界位置应保持稳定。
    /// 删除纹理能量归一化或重新使用固定亮度差会使本测试失败。
    /// </summary>
    /// <param name="smoothBrightness">光滑端头的基础亮度。</param>
    [Theory]
    [InlineData((byte)45)]
    [InlineData((byte)90)]
    [InlineData((byte)145)]
    public void TryLocate_OverallBrightnessChanges_ReturnsSameTextureBoundary(byte smoothBrightness)
    {
        const int expectedBoundaryX = 82;
        using Mat binary = CreateSolidTieMask(180, 30);
        using Mat color = CreateTexturedTie(180, 30, expectedBoundaryX, smoothBrightness, false);
        IRightEndBoundaryLocator locator = new TextureTransitionEndBoundaryLocator();

        bool found = locator.TryLocate(binary, color, new EndBoundaryLocatorOptions(60), out EndBoundaryCandidate candidate);

        Assert.True(found);
        Assert.InRange(candidate.BoundaryX, expectedBoundaryX - 2, expectedBoundaryX + 2);
        Assert.True(candidate.Confidence > 0);
    }

    /// <summary>
    /// 完全光滑的扎带没有结构转折，单根定位器应交给同图共识阶段处理。
    /// 把任意灰度波动都当成边界会使本测试失败。
    /// </summary>
    [Fact]
    public void TryLocate_CompletelySmoothTie_ReturnsFalse()
    {
        using Mat binary = CreateSolidTieMask(180, 30);
        using Mat color = new(30, 180, MatType.CV_8UC3, new Scalar(70, 70, 70));
        IRightEndBoundaryLocator locator = new TextureTransitionEndBoundaryLocator();

        Assert.False(locator.TryLocate(binary, color, new EndBoundaryLocatorOptions(60), out _));
    }

    /// <summary>
    /// 圆头处的局部高亮不能把边界从真实齿纹结束位置移到最右端。
    /// 缺少最小光滑端头长度约束会使本测试失败。
    /// </summary>
    [Fact]
    public void TryLocate_HighlightOnlyAtRoundedTip_DoesNotMoveBoundaryToTip()
    {
        const int expectedBoundaryX = 80;
        using Mat binary = CreateSolidTieMask(180, 30);
        using Mat color = CreateTexturedTie(180, 30, expectedBoundaryX, 70, true);
        IRightEndBoundaryLocator locator = new TextureTransitionEndBoundaryLocator();

        bool found = locator.TryLocate(binary, color, new EndBoundaryLocatorOptions(60), out EndBoundaryCandidate candidate);

        Assert.True(found);
        Assert.InRange(candidate.BoundaryX, expectedBoundaryX - 2, expectedBoundaryX + 2);
    }

    /// <summary>
    /// 在共识预测附近细化时，应吸附到真实纹理结束边缘。
    /// 忽略预测窗口或选择端头反光会使本测试失败。
    /// </summary>
    [Fact]
    public void TryRefineNear_PredictionNearBoundary_ReturnsTextureBoundary()
    {
        const int expectedBoundaryX = 84;
        using Mat binary = CreateSolidTieMask(180, 30);
        using Mat color = CreateTexturedTie(180, 30, expectedBoundaryX, 85, true);
        IRightEndBoundaryLocator locator = new TextureTransitionEndBoundaryLocator();

        bool found = locator.TryRefineNear(
            binary,
            color,
            78,
            15,
            new EndBoundaryLocatorOptions(60),
            out EndBoundaryCandidate candidate);

        Assert.True(found);
        Assert.InRange(candidate.BoundaryX, expectedBoundaryX - 2, expectedBoundaryX + 2);
    }

    /// <summary>
    /// 创建全前景的水平扎带二值掩膜。
    /// </summary>
    /// <param name="width">组件宽度。</param>
    /// <param name="height">组件高度。</param>
    /// <returns>前景为 255 的二值图。</returns>
    private static Mat CreateSolidTieMask(int width, int height)
    {
        return new Mat(height, width, MatType.CV_8UC1, Scalar.White);
    }

    /// <summary>
    /// 创建左侧具有周期齿纹、右侧为光滑端头的合成彩色扎带。
    /// </summary>
    /// <param name="width">组件宽度。</param>
    /// <param name="height">组件高度。</param>
    /// <param name="boundaryX">光滑端头起始列。</param>
    /// <param name="smoothBrightness">光滑区域亮度。</param>
    /// <param name="addTipHighlight">是否在圆头附近加入干扰高亮。</param>
    /// <returns>三通道合成彩色图。</returns>
    private static Mat CreateTexturedTie(
        int width,
        int height,
        int boundaryX,
        byte smoothBrightness,
        bool addTipHighlight)
    {
        Mat color = new(height, width, MatType.CV_8UC3, new Scalar(smoothBrightness, smoothBrightness, smoothBrightness));
        int dark = Math.Max(0, smoothBrightness - 25);
        int bright = Math.Min(255, smoothBrightness + 45);

        for (int x = 0; x < boundaryX; x++)
        {
            int value = x % 6 < 3 ? bright : dark;
            Cv2.Line(color, new Point(x, 0), new Point(x, height - 1), new Scalar(value, value, value));
        }

        if (addTipHighlight)
        {
            Cv2.Rectangle(
                color,
                new Rect(width - 8, height / 3, 4, Math.Max(1, height / 3)),
                new Scalar(220, 220, 220),
                -1);
        }

        return color;
    }

    /// <summary>
    /// 创建先有强齿纹、再有三条弱齿纹、最后进入光滑端头的合成扎带。
    /// </summary>
    /// <param name="width">组件宽度。</param>
    /// <param name="height">组件高度。</param>
    /// <returns>最后弱纹右边缘约位于 X=155 的彩色组件。</returns>
    private static Mat CreateStrongThenWeakTextureTie(int width, int height)
    {
        Mat color = new(height, width, MatType.CV_8UC3, new Scalar(75, 75, 75));

        for (int x = 0; x < 120; x++)
        {
            int value = x % 6 < 3 ? 135 : 30;
            Cv2.Line(color, new Point(x, 0), new Point(x, height - 1), new Scalar(value, value, value));
        }

        foreach (int stripeX in new[] { 132, 142, 152 })
        {
            Cv2.Rectangle(color, new Rect(stripeX, 0, 3, height), new Scalar(100, 100, 100), -1);
        }

        return color;
    }
}
