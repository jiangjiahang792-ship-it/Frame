using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using OpenCvSharp;
using TDJS_Vision.Node._2_ImagePreprocessing.ImageCrop;

/// <summary>根据已记录实际边缘点绘制诊断图，不改变输入图像和测量参数。</summary>
internal static class Station2AngleEvidence
{
    /// <summary>输出同一视角的原始裁剪图及叠加边缘点证据。</summary>
    private static void Main(string[] args)
    {
        var rows=File.ReadLines(args[0]).Select(JObject.Parse).Take(300).ToArray();
        var plan=JObject.Parse(File.ReadAllText(args[1]));
        var station=plan["ProcessInfos"].First(p=>(string)p["ProcessName"]=="工位2主流程");
        var module=station["NodeInfos"].First(n=>(string)n["NodeType"]=="CompositeModule"&&(bool)n["Active"]);
        var nodes=((JObject)module["NodeParam"]).Properties().First().Value["NodeInfos"];
        var crop=nodes.First(n=>(int)n["ID"]==289);
        var roi=((JObject)crop["NodeParam"]).Properties().First().Value["ImageRois"][0].ToObject<ImageCropRoiRegion>();
        foreach(var row in new[]{rows[0],rows[48],rows.First(r=>((string)r["file"]).Contains("10484857-A"))})
        using(var src=Cv2.ImRead((string)row["file"]))
        using(var cropped=roi.CropRectified(src))
        using(var rotated=new Mat())
        {
            Cv2.Rotate(cropped,rotated,RotateFlags.Rotate90Clockwise);
            string name=Path.Combine(args[2],"sample-"+row["index"]);
            Cv2.ImWrite(name+"-raw.png",rotated);
            int j=0;
            foreach(int id in new[]{419,420,422,423})
            {
                var color=new[]{Scalar.Red,Scalar.Blue,Scalar.Green,new Scalar(220,0,220)}[j++];
                var result=row[id.ToString()];
                foreach(var point in result["EdgePoints"]??new JArray())
                    Cv2.Circle(rotated,new Point((double)point["X"],(double)point["Y"]),3,color,-1);
                if((bool?)result["IsOk"]==true)
                {
                    var a=new Point((double)result["StartX"],(double)result["StartY"]);
                    var b=new Point((double)result["EndX"],(double)result["EndY"]);
                    Cv2.Line(rotated,a,b,color,2);
                    Cv2.PutText(rotated,id.ToString(),a,HersheyFonts.HersheySimplex,0.5,color,1);
                }
            }
            Cv2.ImWrite(name+"-edges.png",rotated);
            Console.WriteLine(name);
        }
    }
}
