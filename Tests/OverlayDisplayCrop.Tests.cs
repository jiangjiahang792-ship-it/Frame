using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Newtonsoft.Json;
using OpenCvSharp;
using TDJS_Vision;
using TDJS_Vision.Node;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw;
using TDJS_Vision.Forms.DispShowImage;

/// <summary>截取图像与全部标注共用坐标变换的真实程序集回归。</summary>
internal static class OverlayDisplayCropTests
{
    /// <summary>累计断言数量。</summary>
    private static int checks;
    /// <summary>真实构建器类型。</summary>
    private static readonly Type Builder = typeof(NodeResultOverlayDraw).Assembly.GetType("TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw.ResultOverlayDrawBuilder");
    /// <summary>执行断言。</summary>
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    /// <summary>浮点坐标比较。</summary>
    private static bool Near(float a, float b) => Math.Abs(a-b) < 0.001;
    /// <summary>直接运行截取服务。</summary>
    private static OutputImage Crop(Mat image, AlgorithmResult display, NodeParamResultOverlayDraw p)
        => (OutputImage)Builder.GetMethod("BuildCroppedOutput", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { image, display, new List<Rect>(), p });
    /// <summary>验证任意角度和四象限旋转、关闭兼容、窗体坐标保存。</summary>
    [STAThread]
    private static void Main()
    {
        LanguageManager.Initialize();
        using (var image = new Mat(80,100,MatType.CV_8UC3))
        {
            for(int y=0;y<image.Height;y++) for(int x=0;x<image.Width;x++) image.Set(y,x,new Vec3b((byte)x,(byte)y,100));
            var display = new AlgorithmResult { IsAllOk = false };
            display.Rects.Add(new ColorRotatedRect(40,30,12,8,10,Color.Red));
            display.RectsNgMap["旧NG"] = new List<ColorRotatedRect> {new ColorRotatedRect(40,30,6,4,0,Color.Yellow)};
            display.Texts.Add(new ColorText("位置",Color.Red) { UseImagePosition=true, ImagePosition=new PointF(40,30), FontSize=15 });
            display.Texts.Add(new ColorText("控件右下",Color.Green) { CoordinateMode=DisplayTextCoordinateMode.Control, Position=DisplayTextPosition.BottomRight });
            display.Texts.Add(new ColorText("图像左上",Color.Green) { Position=DisplayTextPosition.TopLeft });
            display.Lines.Add(new ColorLine(new PointF(40,30),new PointF(42,30),Color.Red));
            display.Circles.Add(new ColorCircle(new PointF(40,30),3,Color.Red));
            display.Arcs.Add(new ColorArc(new PointF(40,30),4,20,90,Color.Red));
            display.Ellipses.Add(new ColorEllipse(new PointF(40,30),10,4,15,Color.Red));
            display.Contours.Add(new ColorContour(new List<PointF>{new PointF(40,30),new PointF(42,30)},Color.Red));
            var p = new NodeParamResultOverlayDraw { EnableDisplayCrop=true, DisplayCropRegion = new OverlayDisplayCropRegion { Column=39.5f,Row=29.5f,Length1=20,Length2=10 } };
            foreach(int rotation in new[]{0,90,180,270})
            foreach(float phi in new[]{0f,(float)(Math.PI/2),0.3f})
            {
                p.RotationAngle=rotation; p.DisplayCropRegion.Phi=phi;
                using(var result=Crop(image,display,p))
                {
                    var r=result.DisplayResult;
                    float x=(float)(Math.Cos(phi)*0.5+Math.Sin(phi)*0.5+19.5);
                    float y=(float)(-Math.Sin(phi)*0.5+Math.Cos(phi)*0.5+9.5);
                    float ox=x,oy=y;
                    if(rotation==90){x=19-oy;y=ox;} if(rotation==180){x=39-ox;y=19-oy;} if(rotation==270){x=oy;y=39-ox;}
                    Check(result.SrcImg.Width==(rotation%180==0?40:20) && result.SrcImg.Height==(rotation%180==0?20:40),"输出尺寸");
                    Check(Near(r.Rects[0].RotatedRect.center.x,x)&&Near(r.Rects[0].RotatedRect.center.y,y),"检测框中心");
                    Check(Near(r.Texts[0].ImagePosition.X,x)&&Near(r.Texts[0].ImagePosition.Y,y),"图像坐标文字偏移");
                    Check(r.Texts[1].CoordinateMode==DisplayTextCoordinateMode.Control && r.Texts[1].Position==DisplayTextPosition.BottomRight && !r.Texts[2].UseImagePosition,"角落锚点保留");
                    Check(Near(r.Lines[0].P1.X,x)&&Near(r.Circles[0].Center.Y,y)&&Near(r.Ellipses[0].Center.X,x)&&Near(r.Arcs[0].Center.Y,y)&&Near(r.Contours[0].Points[0].X,x)&&Near(r.RectsNgMap["旧NG"][0].RotatedRect.center.y,y),"各类几何同一变换");
                    Check(!r.IsAllOk && r.ClipToImageBounds,"保留判定并限制图像标注边界");
                    if(phi==0 || phi==(float)(Math.PI/2))
                    {
                        var pixel=result.SrcImg.At<Vec3b>((int)Math.Round(y),(int)Math.Round(x));
                        Check(pixel.Item0==40 && pixel.Item1==30,"图像像素与标注对齐，无一像素偏移");
                    }
                }
            }
            Check(display.Texts[0].ImagePosition.X==40 && display.Rects[0].RotatedRect.center.x==40 && !display.ClipToImageBounds,"上游数据不变");
            p.RotationAngle=0;p.DisplayCropRegion.Phi=0;
            var restored=JsonConvert.DeserializeObject<NodeParamResultOverlayDraw>(JsonConvert.SerializeObject(p));
            Check(restored.EnableDisplayCrop && restored.DisplayCropRegion.Column==39.5,"参数保存恢复");
            p.DisplayCropRegion.Column=-500;
            bool rejected=false;try{using(var bad=Crop(image,display,p)) {}}catch(TargetInvocationException){rejected=true;}
            Check(rejected,"无交集ROI拒绝");
            p.DisplayCropRegion.Column=39.5f;
            var process=new Process("截取回归") { HasCanvasGraph=true };
            using(var source=new NodeBase(1,"图像",process,NodeType.ImageSource))
            using(var owner=new NodeResultOverlayDraw(2,"绘制",process,NodeType.ResultOverlayDraw))
            {
                process.Nodes.AddRange(new[]{source,owner});process.Connections.Add(new ProcessConnection{FromNodeId=1,ToNodeId=2});
                typeof(NodeBase).GetProperty("Result").SetValue(source,new NodeResultImageSource { OutputImage=OutputImage.FromOwnedSingleImage(image.Clone()) });
                source.SetStatus(NodeStatus.Successful,"0");
                p.ImageText1="1.图像";p.ImageText2="输出图像";
                var build=Builder.GetMethods(BindingFlags.Public|BindingFlags.Static).Single(m=>m.Name=="Build"&&m.GetParameters().Length==4);
                foreach(bool enabled in new[]{false,true,false})
                {
                    p.EnableDisplayCrop=enabled;
                    object[] args={owner,p,null,null};build.Invoke(null,args);
                    using(var output=(OutputImage)args[2])
                        Check(output.Bitmaps[0].Width==(enabled?40:100)&&output.Bitmaps[0].Height==(enabled?20:80),"开关恢复完整图像");
                }
                var form=(NodeParamFormResultOverlayDraw)owner.ParamForm;form.Params=p;form.SetParam2Form();
                form.Show();Application.DoEvents();
                using(var bitmap=new Bitmap(form.Width,form.Height)) {form.DrawToBitmap(bitmap,form.ClientRectangle);bitmap.Save("OverlayDisplayCrop-form.png");}
                form.Hide();
            }
            // 验证裁剪仅约束图像标注，且重置显示结果后不会残留裁剪状态。
            using(var control=new ShowImageControl {Size=new System.Drawing.Size(200,100),BackgroundColorCustom=Color.Black})
            using(var white=new Bitmap(100,100))
            using(var graphics=Graphics.FromImage(white))
            {
                graphics.Clear(Color.White);
                var clipped=new AlgorithmResult{ClipToImageBounds=true};
                clipped.Lines.Add(new ColorLine(new PointF(-100,50),new PointF(100,50),Color.Red){LineWidth=3});
                control.SetImage((Bitmap)white.Clone(),clipped);control.ShowFit();
                using(var drawn=new Bitmap(200,100)){control.DrawToBitmap(drawn,new Rectangle(0,0,200,100));Check(drawn.GetPixel(10,50).R<100,"图像边界外不绘制检测线");}
            }
        }
        Console.WriteLine("ROI截取显示："+checks+"项断言通过。");
    }
}
