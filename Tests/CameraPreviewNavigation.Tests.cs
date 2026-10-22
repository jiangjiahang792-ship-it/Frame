using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using TDJS_Vision.Forms.CameraAdd;
/// <summary>真实预览控件离线交互回归。</summary>
class CameraPreviewNavigationTests
{
    /// <summary>暴露受保护鼠标事件，模拟用户操作。</summary>
    class Preview : CameraPreviewPictureBox
    {
        /// <summary>模拟滚轮。</summary>
        public void Wheel(int delta) { OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, 100, 80, delta)); }
        /// <summary>模拟拖动。</summary>
        public void Pan() { OnMouseDown(new MouseEventArgs(MouseButtons.Left,1,100,80,0)); OnMouseMove(new MouseEventArgs(MouseButtons.Left,0,130,100,0)); OnMouseUp(new MouseEventArgs(MouseButtons.Left,1,130,100,0)); }
    }
    /// <summary>读取实际绘制边界。</summary>
    static RectangleF Bounds(Preview p) { return (RectangleF)typeof(CameraPreviewPictureBox).GetMethod("ImageBounds",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,null); }
    /// <summary>校验条件。</summary>
    static void Check(bool b) { if(!b) throw new Exception("预览交互验证失败"); }
    /// <summary>覆盖缩放锚点、连续帧、平移、编辑和重置。</summary>
    [STAThread] static void Main()
    {
        using(var f=new Form()) using(var p=new Preview()) using(var img=new Bitmap(800,600)) using(var frame=new Bitmap(800,600)) using(var target=new Bitmap(400,300))
        {
            p.Size=new Size(400,300); p.Image=img; f.Controls.Add(p); f.Show(); p.DrawToBitmap(target,p.ClientRectangle);
            var before=Bounds(p); p.Wheel(120); var after=Bounds(p);
            Check(after.Width>before.Width); Check(Math.Abs((100-before.X)/before.Width-(100-after.X)/after.Width)<0.0001);
            p.Image=frame; p.DrawToBitmap(target,p.ClientRectangle); Check(Bounds(p)==after);
            p.Pan(); var moved=Bounds(p); Check(Math.Abs(moved.X-after.X-30)<0.01);
            p.NavigationEnabled=false; p.ResetView(); before=Bounds(p); p.Wheel(120); Check(Bounds(p)==before);
            p.NavigationEnabled=true; p.Wheel(120); p.ResetView(); Check(Bounds(p)==before);
            p.Image=null;
        }
        Console.WriteLine("预览缩放锚点、同尺寸连续帧、拖动、ROI编辑禁用缩放和重置验证通过。");
    }
}
