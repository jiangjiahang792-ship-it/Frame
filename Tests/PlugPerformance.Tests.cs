using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenCvSharp;
using TDJS_Vision;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._3_Detection.TDAI.Parse;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

/// <summary>复用已保存的真实模型框测量整栓颜色与匹配耗时，不含读图和YOLO，不访问设备。</summary>
internal static class PlugPerformanceTests
{
    /// <summary>预热后多次运行取中位数，避免首次JIT影响。</summary>
    private static double Median(Action action)
    {
        action();
        var times = new List<double>();
        for (int i=0;i<7;i++) { var watch=Stopwatch.StartNew(); action(); times.Add(watch.Elapsed.TotalMilliseconds); }
        return times.OrderBy(x=>x).ElementAt(times.Count/2);
    }

    /// <summary>参数依次为缓存模型结果、图片目录和输出JSON文件。</summary>
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            var cached = JArray.Parse(File.ReadAllText(args[0])).Where(x=>(int)x["LearningFrames"]==1).ToList();
            var pair = typeof(NewTerminal12Parse).GetMethod("PairWholePlugs",BindingFlags.Static|BindingFlags.NonPublic);
            var colorService = new PlugColorPerimeter();
            var matcher = new PlugContourSimilarity();
            PlugColorModel color = null;
            var report = new List<object>();
            for (int index=0;index<cached.Count;index++)
            {
                var entry=cached[index];
                string name=(string)entry["Image"];
                string path=Path.Combine(args[1],name);
                if(!File.Exists(path)) path=Path.Combine(args[1],"新增NG样图",name);
                var boxes=entry["Boxes"].ToObject<List<DetResult>>();
                var targets=(List<DetResult>)pair.Invoke(null,new object[]{boxes.Where(b=>b.ClassId==5).ToList(),boxes.Where(b=>b.ClassId==6).ToList()});
                using(var image=Cv2.ImRead(path))
                using(var input=new OutputImage{Bitmaps=new List<Mat>{image}})
                {
                    List<PlugPerimeterRegion> regions=null;
                    Action extract=()=> {regions=colorService.Measure(input,targets,ref color).Where(r=>r.Contour.Length>0)
                        .OrderByDescending(r=>r.PixelArea).Take(2).OrderBy(r=>r.Bounds.Y).ToList();};
                    extract();
                    if(index<2)
                    {
                        if(color.ShapeTemplates==null) color.ShapeTemplates=matcher.Learn(regions);
                        else matcher.AddTemplates(color.ShapeTemplates,regions);
                    }
                    double extraction=Median(extract);
                    List<PlugContourAssessment> scores=null;
                    double matching=Median(()=>scores=regions.Select(r=>matcher.Evaluate(r.Contour,color,70)).ToList());
                    const string key="整栓性能验证";
                    Solution.Instance.DetectItemDic[key]=new List<DetectItemInfo>{
                        new DetectItemInfo{Name="防水栓轮廓破损",Enable=true,MinValue="70",MaxValue="100",PlugColor=color},
                        new DetectItemInfo{Name="前防水栓数量",Enable=true,MinValue="2",MaxValue="2"},
                        new DetectItemInfo{Name="后防水栓数量",Enable=true,MinValue="2",MaxValue="2"}};
                    var param=new NodeParamTDAI{CurDetectItemName=key,NodeName=key};
                    var process=new TDJS_Vision.Process(key);
                    var result=new NodeResultTDAI();
                    double total=Median(()=>NewTerminal12Parse.Parse(boxes,boxes.Count,param,process,ref result,input));
                    report.Add(new{Image=name,Templates=color.ShapeTemplates.Count,ColorMs=extraction,MatchMs=matching,ParseMs=total,
                        Scores=scores,Results=result.AlgorithmResult.DetectResults,IsOk=result.IsOk});
                    Console.WriteLine(name+" color="+extraction.ToString("F2")+" match="+matching.ToString("F2")+" parse="+total.ToString("F2"));
                }
            }
            File.WriteAllText(args[2],JsonConvert.SerializeObject(report,Formatting.Indented));
            return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
