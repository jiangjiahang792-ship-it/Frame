using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using OpenCvSharp;
using TDJS_Vision.Node._3_Detection.TDAI.Parse;

/// <summary>两张真实良品图的整栓轮廓重合度试验；不闭运算，不缩放或形变拟合。</summary>
internal static class PlugGoodPair
{
    /// <summary>复用上次独立试验的分割与外轮廓实现。</summary>
    private static Mat Invoke(string name, params object[] args)
    {
        return (Mat)typeof(PlugContourComparison).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);
    }

    /// <summary>按像素面积矩求中心，不依赖背景和线芯。</summary>
    private static Point2f Center(Mat mask)
    {
        var m=Cv2.Moments(mask,true);
        if(m.M00==0) throw new Exception("未分割到防水栓");
        return new Point2f((float)(m.M10/m.M00),(float)(m.M01/m.M00));
    }

    /// <summary>在面积中心初对齐后搜索平移和旋转，输出最大区域IoU及叠加图。</summary>
    private static object Compare(Mat reference,Mat current,string path,bool rotation)
    {
        Point2f a=Center(reference),b=Center(current);
        double best=-1,bestAngle=0;int bx=0,by=0;
        using(var shifted=new Mat()) using(var intersection=new Mat()) using(var union=new Mat())
        using(var bestMask=new Mat())
        {
            for(double angle=rotation?-10:0;angle<=(rotation?10:0);angle+=.5)
            using(var transform=Cv2.GetRotationMatrix2D(b,angle,1))
            {
                double tx=transform.At<double>(0,2)+a.X-b.X,ty=transform.At<double>(1,2)+a.Y-b.Y;
                // 缺口会移动面积中心，统一扩大搜索以避免最优位置停在旧的±6边界。
                for(int dy=-12;dy<=12;dy++)for(int dx=-12;dx<=12;dx++)
                {
                    transform.Set<double>(0,2,tx+dx);transform.Set<double>(1,2,ty+dy);
                    Cv2.WarpAffine(current,shifted,transform,reference.Size(),InterpolationFlags.Nearest,BorderTypes.Constant,Scalar.Black);
                    Cv2.BitwiseAnd(reference,shifted,intersection);Cv2.BitwiseOr(reference,shifted,union);
                    double score=100.0*Cv2.CountNonZero(intersection)/Cv2.CountNonZero(union);
                    if(score>best){best=score;bestAngle=angle;bx=dx;by=dy;shifted.CopyTo(bestMask);}
                }
            }
            using(var output=new Mat())using(var onlyReference=new Mat())using(var onlyCurrent=new Mat())
            {
                Cv2.CvtColor(reference,output,ColorConversionCodes.GRAY2BGR);
                Cv2.Subtract(reference,bestMask,onlyReference);Cv2.Subtract(bestMask,reference,onlyCurrent);
                output.SetTo(new Scalar(0,0,255),onlyReference);output.SetTo(new Scalar(255,255,0),onlyCurrent);
                Cv2.ImWrite(path,output);
            }
            return new { Similarity=best,Angle=bestAngle,CenterOffsetX=a.X-b.X,CenterOffsetY=a.Y-b.Y,FineX=bx,FineY=by,
                ReferenceArea=Cv2.CountNonZero(reference),CurrentArea=Cv2.CountNonZero(current)};
        }
    }

    /// <summary>以图2作为模板、图1作为待测图；保持统一颜色参数，输出整栓上下两项。</summary>
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            string dir=args[0];Directory.CreateDirectory(dir);
            var result=new List<object>();
            var color=new PlugColorModel {Hue=52,HueTolerance=10,MinS=31,MaxS=255,MinV=8,MaxV=203};
            Rect[] boxes={new Rect(155,105,540,415),new Rect(145,550,550,430)};
            using(var first=Cv2.ImRead(Path.Combine(dir,"image1.jpg")))
            using(var second=Cv2.ImRead(Path.Combine(dir,"image2.jpg")))
            for(int i=0;i<boxes.Length;i++)
            using(var raw1=Invoke("Segment",first,boxes[i],color))
            using(var raw2=Invoke("Segment",second,boxes[i],color))
            using(var shape1=Invoke("Silhouette",raw1))
            using(var shape2=Invoke("Silhouette",raw2))
            {
                Cv2.ImWrite(Path.Combine(dir,"mask1-"+i+".png"),raw1);
                Cv2.ImWrite(Path.Combine(dir,"mask2-"+i+".png"),raw2);
                result.Add(new {Position=i==0?"上方整栓":"下方整栓",Roi=boxes[i],
                    Translation=Compare(shape2,shape1,Path.Combine(dir,"translation-"+i+".png"),false),
                    Rigid=Compare(shape2,shape1,Path.Combine(dir,"rigid-"+i+".png"),true)});
            }
            string json=JsonConvert.SerializeObject(result,Formatting.Indented);
            File.WriteAllText(Path.Combine(dir,"results.json"),json);Console.WriteLine(json);return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
