using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using TDJS_Vision.Forms.DispShowImage;
using TDJS_Vision.Node._3_Detection.TDAI;

/// <summary>验证无效辅助图形、绘图状态泄漏及布局变化不能吞掉控件坐标文字。</summary>
internal static class ShowImageOverlayIsolationTests
{
    /// <summary>使用真实控件与GDI绘制，不连接任何设备。</summary>
    [STAThread]
    private static void Main()
    {
        // 有限坐标的小夹角真实复现：修复前0.28倍文字消失，1倍恢复。
        foreach (float sweep in new[] { 0.101F, 0.153F, 0.779F, 30F })
        using (var control = new ShowImageControl())
        {
            control.Size = new Size(500, 440);
            control.CreateControl();
            control.ShowStatusBar = false;
            var result = new AlgorithmResult();
            result.Arcs.Add(new ColorArc(new PointF(250, 1000), 120, 90, sweep, Color.Red));
            result.Texts.Add(new ColorText("角度：" + sweep, Color.Lime) {
                Title = "", CoordinateMode = DisplayTextCoordinateMode.Control });
            result.Texts.Add(new ColorText("OK", Color.Lime) {
                Title = "", Position = DisplayTextPosition.BottomRight,
                CoordinateMode = DisplayTextCoordinateMode.Control });
            control.SetImage(new Bitmap(750, 1550), result);
            for (int repeat = 0; repeat < 3; repeat++)
            {
                control.ShowFit();
                AssertText(control);
                foreach (float scale in new[] { 0.1F, 0.28F, 0.5F, 1F, 2F })
                {
                    SetField(control, "_fitViewEnabled", false);
                    SetField(control, "_scale", scale);
                    SetField(control, "_offset", PointF.Empty);
                    AssertText(control);
                }
            }
        }
        Console.WriteLine("通过72组小夹角及普通圆弧缩放/自适应实际绘图检查。");
        foreach (Size size in new[] { new Size(500, 440), new Size(1000, 800) })
        foreach (float coordinate in new[] { 1000000000f, float.NaN, float.PositiveInfinity })
        using (var control = new ShowImageControl())
        {
            control.Size = size;
            control.CreateControl();
            control.ShowStatusBar = false;
            var result = new AlgorithmResult();
            result.Circles.Add(new ColorCircle(new PointF(250, coordinate), 8, Color.Red));
            result.Lines.Add(new ColorLine(new PointF(250, 100), new PointF(250, coordinate), Color.Red));
            result.Texts.Add(new ColorText("角度：0.096", Color.Lime) {
                Title = "", CoordinateMode = DisplayTextCoordinateMode.Control
            });
            result.Texts.Add(new ColorText("0°", Color.Lime) {
                Title = "", Position = DisplayTextPosition.BottomRight,
                CoordinateMode = DisplayTextCoordinateMode.Control
            });
            control.SetImage(new Bitmap(750, 1550), result);
            AssertText(control);
            // 让异常发生在画布已经改变之后，检查后续文字是否仍位于控件四角。
            var rois = (List<ShowImageControl.IRoiShape>)typeof(ShowImageControl)
                .GetField("_staticRois", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(control);
            rois.Insert(0, new FailingTransformedRoi());
            control.Size = Size.Empty;
            control.ShowFit();
            control.Size = size;
            AssertText(control);
            SetField(control, "_fitViewEnabled", false);
            SetField(control, "_scale", 0.6f);
            SetField(control, "_offset", new PointF(30, -200));
            AssertText(control);
        }
        Console.WriteLine("通过18组实际绘图：异常圆/线段、画布恢复、窗口尺寸恢复及手动平移后四角文字可见。");
    }

    /// <summary>仅测试中设置视口，模拟用户缩放和平移。</summary>
    private static void SetField(ShowImageControl control, string name, object value)
    {
        typeof(ShowImageControl).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(control, value);
    }

    /// <summary>验证两个独立文字区域，避免把中间的辅助图形误判成文字。</summary>
    private static void AssertText(ShowImageControl control)
    {
        using (var image = new Bitmap(control.Width, control.Height))
        {
            control.DrawToBitmap(image, control.ClientRectangle);
            foreach (var area in new[] {
                new Rectangle(0, 0, 200, 70),
                new Rectangle(control.Width-100, control.Height-100, 100, 100) })
            {
                int green = 0;
                for (int y=area.Top;y<area.Bottom;y++)
                    for (int x=area.Left;x<area.Right;x++) {
                        Color pixel=image.GetPixel(x,y);
                        if(pixel.G>pixel.R+20 && pixel.G>pixel.B+20) green++;
                    }
                if(green<20) throw new Exception("控件角落文字消失："+area);
            }
        }
    }

    /// <summary>模拟绘制辅助图形时改变变换和裁剪后出现GDI参数错误。</summary>
    private sealed class FailingTransformedRoi : ShowImageControl.IRoiShape
    {
        /// <summary>选中状态。</summary>
        public bool IsSelected { get; set; }
        /// <summary>静态标记。</summary>
        public bool IsStatic { get; set; }
        /// <summary>图形标题。</summary>
        public string Label { get; set; }
        /// <summary>图形颜色。</summary>
        public Color ShapeColor { get; set; }
        /// <summary>改变画布后模拟参数异常。</summary>
        public void Draw(Graphics graphics, float scale, PointF offset)
        {
            graphics.TranslateTransform(10000, 10000);
            graphics.SetClip(new Rectangle(0, 0, 1, 1));
            throw new ArgumentException("模拟辅助图形绘制失败");
        }
        /// <summary>测试图形不参加命中检测。</summary>
        public int HitTest(Point point, float scale, PointF offset) => -1;
        /// <summary>测试图形不参加拖动。</summary>
        public void BeginDrag(PointF point) { }
        /// <summary>测试图形不参加拖动。</summary>
        public void DragTo(PointF point, int handle) { }
        /// <summary>返回默认中心。</summary>
        public PointF GetCenter() => PointF.Empty;
    }
}
