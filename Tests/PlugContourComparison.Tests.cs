using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using OpenCvSharp;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._3_Detection.TDAI.Parse;

/// <summary>独立无闭运算轮廓对比试验，不接入生产解析器。</summary>
internal static class PlugContourComparison
{
    /// <summary>沿用上次试验的人工ROI，顺序为上前、上后、下前、下后。</summary>
    private static readonly Rect[] Regions = { new Rect(445,185,220,265), new Rect(180,120,265,390),
        new Rect(420,610,225,270), new Rect(155,560,265,400) };

    /// <summary>学习颜色分割并保留至少2000像素的连通域，不进行任何闭运算。</summary>
    private static Mat Segment(Mat image, Rect roi, PlugColorModel color)
    {
        var mask = new Mat();
        using (var crop = new Mat(image, roi))
        using (var hsv = new Mat())
        using (var labels = new Mat())
        using (var stats = new Mat())
        using (var centers = new Mat())
        {
            Cv2.CvtColor(crop, hsv, ColorConversionCodes.BGR2HSV);
            Cv2.InRange(hsv, new Scalar(color.Hue-color.HueTolerance,color.MinS,color.MinV),
                new Scalar(color.Hue+color.HueTolerance,color.MaxS,color.MaxV),mask);
            int count = Cv2.ConnectedComponentsWithStats(mask,labels,stats,centers);
            var keep = new bool[count];
            for(int i=1;i<count;i++) keep[i]=stats.At<int>(i,(int)ConnectedComponentsTypes.Area)>=2000;
            var label = labels.GetGenericIndexer<int>();
            var pixels = mask.GetGenericIndexer<byte>();
            for(int y=0;y<mask.Height;y++) for(int x=0;x<mask.Width;x++) pixels[y,x]=keep[label[y,x]]?(byte)255:(byte)0;
        }
        return mask;
    }

    /// <summary>把外轮廓填实用于纯形状比较；原始掩膜另外保留，以检查内部裂纹。</summary>
    private static Mat Silhouette(Mat mask)
    {
        var filled = new Mat(mask.Size(),MatType.CV_8UC1,Scalar.Black);
        Cv2.FindContours(mask,out Point[][] contours,out HierarchyIndex[] hierarchy,RetrievalModes.External,ContourApproximationModes.ApproxSimple);
        Cv2.DrawContours(filled,contours,-1,Scalar.White,-1);
        return filled;
    }

    /// <summary>仅平移对齐，不拉伸形状；在±12像素内寻找外轮廓区域最大重合位置。</summary>
    private static Point Align(Mat reference, Mat current)
    {
        int best=int.MaxValue; var offset=new Point();
        using(var shifted=new Mat()) using(var difference=new Mat())
        using(var transform=Mat.Eye(2,3,MatType.CV_64FC1).ToMat())
        {
            for(int dy=-12;dy<=12;dy++) for(int dx=-12;dx<=12;dx++)
            {
                transform.Set<double>(0,2,dx); transform.Set<double>(1,2,dy);
                Cv2.WarpAffine(current,shifted,transform,current.Size(),InterpolationFlags.Nearest,BorderTypes.Constant,Scalar.Black);
                Cv2.BitwiseXor(reference,shifted,difference);
                int score=Cv2.CountNonZero(difference);
                if(score<best || (score==best && dx*dx+dy*dy<offset.X*offset.X+offset.Y*offset.Y))
                {best=score;offset=new Point(dx,dy);}
            }
        }
        return offset;
    }

    /// <summary>按确定的平移量转换掩膜。</summary>
    private static Mat Shift(Mat mask,Point offset)
    {
        var output=new Mat();
        using(var transform=Mat.Eye(2,3,MatType.CV_64FC1).ToMat())
        {
            transform.Set<double>(0,2,offset.X); transform.Set<double>(1,2,offset.Y);
            Cv2.WarpAffine(mask,output,transform,mask.Size(),InterpolationFlags.Nearest,BorderTypes.Constant,Scalar.Black);
        }
        return output;
    }

    /// <summary>计算最大独立差异块的真实像素面积。</summary>
    private static int Largest(Mat mask)
    {
        using(var labels=new Mat()) using(var stats=new Mat()) using(var centers=new Mat())
        {
            int count=Cv2.ConnectedComponentsWithStats(mask,labels,stats,centers),max=0;
            for(int i=1;i<count;i++) max=Math.Max(max,stats.At<int>(i,(int)ConnectedComponentsTypes.Area));
            return max;
        }
    }

    /// <summary>运行试验，3像素边缘容差只用于比较，未修改分割轮廓。</summary>
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            string dir=Path.GetFullPath(args[0]),output=Path.Combine(dir,"contour-no-close");
            Directory.CreateDirectory(output);
            var items=JsonConvert.DeserializeObject<List<DetectItemInfo>>(File.ReadAllText(Path.Combine(dir,"learned-5.items")));
            var results=new List<object>();
            using(var original=Cv2.ImRead(Path.Combine(dir,"original.jpg")))
            using(var kernel=Cv2.GetStructuringElement(MorphShapes.Ellipse,new Size(7,7)))
            foreach(string file in new[]{"original.jpg","small-notch.png","large-tear.png","surface-crack.png"})
            using(var source=Cv2.ImRead(Path.Combine(dir,file))) using(var image=new Mat())
            {
                Cv2.Resize(source,image,original.Size(),0,0,InterpolationFlags.Area);
                for(int i=0;i<Regions.Length;i++)
                using(var reference=Segment(original,Regions[i],items[i%2].PlugColor))
                using(var current=Segment(image,Regions[i],items[i%2].PlugColor))
                using(var refShape=Silhouette(reference)) using(var curShape=Silhouette(current))
                {
                    Point offset=Align(refShape,curShape);
                    using(var aligned=Shift(current,offset)) using(var alignedShape=Shift(curShape,offset))
                    using(var intersection=new Mat()) using(var union=new Mat())
                    using(var inner=new Mat()) using(var inverse=new Mat()) using(var missing=new Mat())
                    using(var allMissing=new Mat()) using(var overlay=new Mat())
                    {
                        Cv2.BitwiseAnd(refShape,alignedShape,intersection); Cv2.BitwiseOr(refShape,alignedShape,union);
                        double iou=100.0*Cv2.CountNonZero(intersection)/Math.Max(1,Cv2.CountNonZero(union));
                        // 只把偏离正常边缘至少3像素的缺失纳入局部缺损，抑制分割锯齿。
                        Cv2.Erode(refShape,inner,kernel);
                        Cv2.BitwiseNot(alignedShape,inverse); Cv2.BitwiseAnd(inner,inverse,missing);
                        // 内部裂纹通道：原图有材料、当前图没材料，且位于模板边缘容差以内部位。
                        Cv2.BitwiseAnd(inner,reference,inner);
                        Cv2.BitwiseNot(aligned,inverse); Cv2.BitwiseAnd(inner,inverse,allMissing);
                        int externalLargest=Largest(missing),allLargest=Largest(allMissing);
                        results.Add(new {Image=file,Region=i,Position=i<2?"上方":"下方对照",Item=items[i%2].Name,
                            OffsetX=offset.X,OffsetY=offset.Y,Similarity=iou,
                            ExternalMissing=Cv2.CountNonZero(missing),ExternalLargest=externalLargest,
                            MaterialMissing=Cv2.CountNonZero(allMissing),MaterialLargest=allLargest});
                        Cv2.CvtColor(aligned,overlay,ColorConversionCodes.GRAY2BGR);
                        overlay.SetTo(new Scalar(0,0,255),missing);
                        using(var insideMissing=new Mat())
                        {
                            Cv2.Subtract(allMissing,missing,insideMissing);
                            overlay.SetTo(new Scalar(0,255,255),insideMissing);
                        }
                        Cv2.ImWrite(Path.Combine(output,Path.GetFileNameWithoutExtension(file)+"-"+i+".png"),overlay);
                    }
                }
            }
            File.WriteAllText(Path.Combine(output,"results.json"),JsonConvert.SerializeObject(results,Formatting.Indented));
            Console.WriteLine("无闭运算对比完成："+output);return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
