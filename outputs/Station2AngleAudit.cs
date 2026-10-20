using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TDJS_Vision;
using TDJS_Vision.Node;
using TDJS_Vision.Forms.ProcessNew;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._2_ImagePreprocessing.ImageRotate;
using OpenCvSharp;

/// <summary>只恢复角度依赖节点，使用实际程序集离线诊断，不执行任何设备通信或保存方案。</summary>
internal static class Station2AngleAudit
{
    /// <summary>显式按依赖顺序运行，杜绝其它检测及通信节点进入测试。</summary>
    private static readonly int[] Order = {89,286,289,299,302,382,417,383,418,384,385,387,388,419,420,422,423,386,389,421,424,390,425};
    /// <summary>只记录数值、判定和错误信息，避免序列化图像资源或窗体。</summary>
    private static object Snapshot(object obj, int depth=0)
    {
        if(obj==null || depth>4)return null;
        Type type=obj.GetType();
        if(type.IsPrimitive || obj is string || obj is decimal || type.IsEnum)return obj;
        if(obj is IEnumerable && !(obj is string))return ((IEnumerable)obj).Cast<object>().Take(40).Select(x=>Snapshot(x,depth+1)).ToArray();
        var result=new JObject();
        foreach(var p in type.GetProperties().Where(p=>p.CanRead&&p.GetIndexParameters().Length==0))
        {
            if(p.Name.Contains("Image")||p.Name.Contains("Roi")||p.Name.Contains("ROI")||p.Name.Contains("Bitmap"))continue;
            try { var v=Snapshot(p.GetValue(obj),depth+1);if(v!=null)result[p.Name]=JToken.FromObject(v); }catch{}
        }
        return result;
    }
    /// <summary>构造隔离检测流程并逐图输出可追溯的节点诊断。</summary>
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Environment.SetEnvironmentVariable("TDJS_VISION_PERFORMANCE_ACCEPTANCE","1");
            Environment.SetEnvironmentVariable("TDJS_VISION_BACKGROUND_ACCEPTANCE","1");
            LanguageManager.Initialize();
            var plan=JObject.Parse(File.ReadAllText(args[0]));
            var original=(JObject)plan["ProcessInfos"].First(p=>(string)p["ProcessName"]=="工位2主流程");
            var module=original["NodeInfos"].First(n=>(string)n["NodeType"]=="CompositeModule"&&(bool)n["Active"]);
            var inner=(JArray)((JObject)module["NodeParam"]).Properties().First().Value["NodeInfos"];
            var config=(JObject)original.DeepClone();
            var source=(JObject)original["NodeInfos"].First(n=>(int)n["ID"]==74).DeepClone();
            source["ID"]=89;source["NodeName"]="离线图像";
            var chosen=new JArray(source);
            foreach(var id in Order.Skip(1))
            {
                var n=(JObject)inner.First(entry=>(int)entry["ID"]==id).DeepClone();
                if(id==286||id==289){var p=((JObject)n["NodeParam"]).Properties().First().Value;p["Text1"]="89.离线图像";p["Text2"]="输出图像";}
                chosen.Add(n);
            }
            // 上游订阅校验依赖流程连线；串行诊断链仅建立已保留节点之间的依赖。
            var connections=new JArray();
            for(int k=1;k<Order.Length;k++)connections.Add(new JObject{["ID"]=Guid.NewGuid().ToString("N"),["FromNodeId"]=Order[k-1],["ToNodeId"]=Order[k],["FromAnchor"]="Bottom",["ToAnchor"]="Top",["Branch"]="Default"});
            config["NodeInfos"]=chosen;config["ConnectionInfos"]=connections;config["ProcessName"]="工位2角度独立诊断";
            using(var editor=new ProcessEditPanel((string)config["ProcessName"],false,config.ToObject<ProcessConfig>()))
            {
                var nodes=editor.RootProcess.Nodes.ToDictionary(n=>n.ID);
                var param=(NodeParamImageSoucre)nodes[89].ParamForm.Params;
                string[] files=Directory.GetFiles(args[1],"*.jpg").OrderBy(p=>p).Take(int.Parse(args[3])).ToArray();
                // 尾部重复首张图五次，单独检查相同像素输入的算法重复性。
                bool affine=args.Length>4&&args[4]=="affine";
                if(affine)files=new[]{files[0],files[48],files[135]}.SelectMany(f=>Enumerable.Repeat(f,7)).ToArray();
                else if(files.Length>3)files=files.Concat(Enumerable.Repeat(files[0],5)).ToArray();
                using(var writer=new StreamWriter(args[2],false,new System.Text.UTF8Encoding(false)))
                for(int i=0;i<files.Length;i++)
                {
                    param.ImageSource="本地图像";param.ImagePath=files[i];
                    var row=new JObject {["file"]=files[i],["index"]=i};
                    // 在已裁剪、旋转的同一图像上施加已知刚体变换，再验证匹配和修正。
                    double angle=affine?new double[]{0,0,0,5,-5,10,-10}[i%7]:0;
                    double dx=affine?new double[]{0,20,-20,0,0,15,-15}[i%7]:0;
                    double dy=affine?new double[]{0,30,-30,0,0,-20,20}[i%7]:0;
                    row["knownAngle"]=angle;row["dx"]=dx;row["dy"]=dy;
                    foreach(int id in Order)
                    {
                        try {
                            nodes[id].Run(CancellationToken.None,false).GetAwaiter().GetResult();
                            if(affine&&(id==299||id==302)){
                                var output=((NodeResultImageRotate)nodes[id].Result).OutputImage;
                                var src=output.SrcImg;
                                using(var matrix=Cv2.GetRotationMatrix2D(new Point2f(src.Width/2f,src.Height/2f),-angle,1)){
                                    matrix.Set(0,2,matrix.At<double>(0,2)+dx);matrix.Set(1,2,matrix.At<double>(1,2)+dy);
                                    var warped=new Mat();Cv2.WarpAffine(src,warped,matrix,src.Size(),InterpolationFlags.Linear,BorderTypes.Constant,Scalar.White);
                                    var gray=new Mat();Cv2.CvtColor(warped,gray,ColorConversionCodes.BGR2GRAY);
                                    ((NodeResultImageRotate)nodes[id].Result).OutputImage=new OutputImage{SrcImg=warped,GrayImg=gray,Bitmaps=new List<Mat>{warped}};
                                    row["matrix"+id]=new JArray(matrix.At<double>(0,0),matrix.At<double>(0,1),matrix.At<double>(0,2),matrix.At<double>(1,0),matrix.At<double>(1,1),matrix.At<double>(1,2));
                                }
                            }
                            row[id.ToString()]=JToken.FromObject(Snapshot(nodes[id].Result));
                        }
                        catch(Exception ex){row[id.ToString()]=new JObject{["error"]=ex.Message};}
                    }
                    writer.WriteLine(row.ToString(Formatting.None));writer.Flush();
                    Console.WriteLine((i+1)+"/"+files.Length+" "+Path.GetFileName(files[i])+" "+row["390"]?["Angle"]+" / "+row["425"]?["Angle"]);
                }
            }
            return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
