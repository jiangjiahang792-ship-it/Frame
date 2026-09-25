using System.Drawing;
using TieCornerInspection.WinForms.Controls;
using Xunit;

namespace TieCornerInspection.Tests;

/// <summary>
/// 验证图像缩放视口的坐标变换行为。
/// </summary>
public sealed class ImageViewportTests
{
    /// <summary>
    /// 验证适应窗口时保持图像宽高比并在垂直方向居中。
    /// 若适应比例错误或图像贴到左上角，本测试应失败。
    /// </summary>
    [Fact]
    public void SetViewport_FitMode_PreservesAspectRatioAndCentersImage()
    {
        ImageViewport viewport = new();

        viewport.SetViewport(new Size(500, 500), new Size(1000, 500));

        Assert.Equal(0.5, viewport.Scale, 6);
        AssertClose(0F, viewport.ImageRectangle.X);
        AssertClose(125F, viewport.ImageRectangle.Y);
        AssertClose(500F, viewport.ImageRectangle.Width);
        AssertClose(250F, viewport.ImageRectangle.Height);
    }

    /// <summary>
    /// 验证以鼠标点缩放时，该鼠标点对应的图像坐标保持不变。
    /// 若缩放错误地围绕控件中心执行，本测试应失败。
    /// </summary>
    [Fact]
    public void ZoomAt_ClientAnchor_KeepsImagePointUnderCursor()
    {
        ImageViewport viewport = new();
        viewport.SetViewport(new Size(500, 500), new Size(1000, 500));
        PointF anchor = new(250, 250);
        PointF before = viewport.ClientToImage(anchor);

        viewport.ZoomAt(anchor, 2.0);
        PointF after = viewport.ClientToImage(anchor);

        AssertClose(before.X, after.X);
        AssertClose(before.Y, after.Y);
        Assert.Equal(1.0, viewport.Scale, 6);
    }

    /// <summary>
    /// 验证连续缩放不会越过 10% 到 2000% 的用户缩放范围。
    /// 若缺少限制导致数值溢出或图像消失，本测试应失败。
    /// </summary>
    [Fact]
    public void ZoomAt_RepeatedZoom_ClampsUserZoomFactor()
    {
        ImageViewport viewport = new();
        viewport.SetViewport(new Size(500, 500), new Size(1000, 500));

        viewport.ZoomAt(new PointF(250, 250), 1000);
        Assert.Equal(20.0, viewport.UserZoomFactor, 6);

        viewport.ZoomAt(new PointF(250, 250), 0.00001);
        Assert.Equal(0.1, viewport.UserZoomFactor, 6);
    }

    /// <summary>
    /// 验证平移和复位对目标矩形产生可观察的正确结果。
    /// 若复位未清除用户平移或缩放，本测试应失败。
    /// </summary>
    [Fact]
    public void PanAndReset_ModifiesThenRestoresFitRectangle()
    {
        ImageViewport viewport = new();
        viewport.SetViewport(new Size(500, 500), new Size(1000, 500));
        viewport.ZoomAt(new PointF(250, 250), 2.0);

        viewport.PanBy(40, -25);
        AssertClose(-210F, viewport.ImageRectangle.X);
        AssertClose(-25F, viewport.ImageRectangle.Y);

        viewport.Reset();
        Assert.Equal(0.5, viewport.Scale, 6);
        AssertClose(0F, viewport.ImageRectangle.X);
        AssertClose(125F, viewport.ImageRectangle.Y);
    }

    /// <summary>
    /// 断言两个单精度坐标在一个千分之一像素内相等。
    /// </summary>
    /// <param name="expected">手工推导的期望坐标。</param>
    /// <param name="actual">实际坐标。</param>
    private static void AssertClose(float expected, float actual)
    {
        Assert.InRange(actual, expected - 0.001F, expected + 0.001F);
    }
}
