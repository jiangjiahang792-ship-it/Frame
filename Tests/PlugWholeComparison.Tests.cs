using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using OpenCvSharp;
using TDJS_Vision.Node._3_Detection.TDAI.Parse;

/// <summary>统一整栓ROI、颜色及刚性对齐比较良品和合成缺陷；仅为独立试验。</summary>
internal static class PlugWholeComparison
{
    /// <summary>复用先前实测方法，避免为缺陷图另调算法或阈值。</summary>
    private static object Invoke(Type type,string name,params object[] args)
    {
        return type.GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);
    }

    /// <summary>图2作为统一模板；只将生成器输出归一到模板分辨率，不拟合目标尺寸。</summary>
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            string root=args[0], output=Path.Combine(root,"outputs/PlugWholeComparison-20261018");
            Directory.CreateDirectory(output);
            string good=Path.Combine(root,"outputs/PlugGoodPair-20261018");
            string defects=Path.Combine(root,"outputs/PlugColorSamples-20261018");
            string[] files={Path.Combine(good,"image2.jpg"),Path.Combine(good,"image1.jpg"),
                Path.Combine(defects,"small-notch.png"),Path.Combine(defects,"large-tear.png"),Path.Combine(defects,"surface-crack.png")};
            var color=new PlugColorModel {Hue=52,HueTolerance=10,MinS=31,MaxS=255,MinV=8,MaxV=203};
            Rect[] boxes={new Rect(155,105,540,415),new Rect(145,550,550,430)};
            var results=new List<object>();
            using(var template=Cv2.ImRead(files[0]))
            foreach(string file in files)
            using(var source=Cv2.ImRead(file)) using(var image=new Mat())
            {
                if(source.Size()==template.Size())source.CopyTo(image);
                else Cv2.Resize(source,image,template.Size(),0,0,InterpolationFlags.Area);
                string name=Path.GetFileNameWithoutExtension(file);
                for(int i=0;i<boxes.Length;i++)
                using(var reference=(Mat)Invoke(typeof(PlugContourComparison),"Segment",template,boxes[i],color))
                using(var current=(Mat)Invoke(typeof(PlugContourComparison),"Segment",image,boxes[i],color))
                using(var refShape=(Mat)Invoke(typeof(PlugContourComparison),"Silhouette",reference))
                using(var curShape=(Mat)Invoke(typeof(PlugContourComparison),"Silhouette",current))
                {
                    Cv2.ImWrite(Path.Combine(output,name+"-mask-"+i+".png"),current);
                    object measured=Invoke(typeof(PlugGoodPair),"Compare",refShape,curShape,
                        Path.Combine(output,name+"-aligned-"+i+".png"),true);
                    results.Add(new {Image=name,Input=file,SourceWidth=source.Width,SourceHeight=source.Height,
                        Position=i==0?"上方整栓":"下方整栓",Roi=boxes[i],Measurement=measured});
                }
            }
            File.WriteAllText(Path.Combine(output,"results.json"),JsonConvert.SerializeObject(results,Formatting.Indented));
            Console.WriteLine("整栓统一对比完成："+output);return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
