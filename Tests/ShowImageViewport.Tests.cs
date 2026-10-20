using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using TDJS_Vision.Forms.DispShowImage;

/// <summary>验证布局恢复、旋转后尺寸切换及手动缩放时的实际位图绘制。</summary>
internal static class ShowImageViewportTests
{
    /// <summary>在STA线程运行控件回归测试。</summary>
    [STAThread]
    private static int Main()
    {
        using (var control = new ShowImageControl())
        {
            control.Size = new Size(400, 300);
            control.ShowStatusBar = false;
            control.CreateControl();
            control.SetImage(CreateImage(800, 400));
            AssertRendered(control);
            control.Size = Size.Empty;
            control.ShowFit();
            control.Size = new Size(400, 300);
            AssertRendered(control);
            for (int i = 0; i < 100; i++)
            {
                var image = CreateImage(800, 400);
                image.RotateFlip(RotateFlipType.Rotate270FlipNone);
                control.SetImage(image);
                control.ShowFit();
                control.Size = new Size(200, 150);
                control.Size = new Size(400, 300);
                AssertRendered(control);
            }
            typeof(Control).GetMethod("OnMouseWheel", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(control, new object[] { new MouseEventArgs(MouseButtons.None, 0, 200, 150, 120) });
            var point = control.ImageToScreen(new PointF(50, 50));
            control.Size = new Size(500, 350);
            if (point != control.ImageToScreen(new PointF(50, 50)))
                throw new Exception("手动缩放被布局恢复覆盖");
            control.ShowFit();
            AssertRendered(control);
        }
        Console.WriteLine("通过：零尺寸恢复、100轮270度旋转和布局切换实际绘图、手动缩放保持。");
        return 0;
    }

    /// <summary>创建颜色已知的测试帧，由控件接管释放。</summary>
    private static Bitmap CreateImage(int width, int height)
    {
        var bitmap = new Bitmap(width, height);
        using (var graphics = Graphics.FromImage(bitmap)) graphics.Clear(Color.Lime);
        return bitmap;
    }

    /// <summary>确认图像完整落入视口且中心像素实际可见。</summary>
    private static void AssertRendered(ShowImageControl control)
    {
        using (var output = new Bitmap(control.Width, control.Height))
        {
            control.DrawToBitmap(output, control.ClientRectangle);
            if (output.GetPixel(control.Width / 2, control.Height / 2).G < 200)
                throw new Exception("图像中心消失");
        }
        var start = control.ImageToScreen(PointF.Empty);
        var end = control.ImageToScreen(new PointF(control.Image.Width, control.Image.Height));
        if (float.IsNaN(start.X) || start.X < -0.1 || start.Y < -0.1
            || end.X > control.Width + 0.1 || end.Y > control.Height + 0.1
            || end.X <= start.X || end.Y <= start.Y)
            throw new Exception("自适应变换无效或图像超出视口");
    }
}
