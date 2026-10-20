using System;
using System.Collections.Generic;
using System.Globalization;
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

/// <summary>加载实际方案的AI模型及ROI，仅离线运行图片，不启动设备或修改现场方案。</summary>
internal static class PlugLocalContourSamples
{
    /// <summary>实际推理后的图片和独立模型框缓存，保证各比较方案使用同一组框。</summary>
    private sealed class Sample
    {
        /// <summary>输入图片绝对路径。</summary>
        public string Path;
        /// <summary>实际模型输出框。</summary>
        public List<DetResult> Boxes;
    }

    /// <summary>运行实际模型、单模板及双正常样本学习对照；隔离worker入口复用主程序集。</summary>
    [STAThread]
    private static int Main(string[] args)
    {
        var worker = typeof(NodeTDAI).Assembly.GetType("TDJS_Vision.Node._3_Detection.TDAI.Yolo8.YoloIsolatedWorkerProgram");
        if ((bool)worker.GetMethod("TryRun", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { args })) return 0;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            var sol = JObject.Parse(File.ReadAllText(args[0]));
            var node = sol.Descendants().OfType<JProperty>().First(p => p.Name == typeof(NodeParamTDAI).FullName);
            var settings = node.Value.ToObject<NodeParamTDAI>();
            var ai = JsonConvert.DeserializeObject<AIInputInfo>(StringCipher.Decrypt(File.ReadAllText(settings.ConfigPath)));
            Directory.CreateDirectory(args[2]);
            var samples = new List<Sample>();
            var paths = new[] { System.IO.Path.Combine(args[1], "original.jpg"), System.IO.Path.Combine(args[1], "good1.jpg") }
                .Concat(Directory.GetFiles(System.IO.Path.Combine(args[1], "新增NG样图"), "*.png")).ToArray();
            var detector = new Yolo8Det();
            try
            {
                detector.Init(ai.ModelInfo.ModelPath, ai.ModelInfo.DeviceType, ai.ClassNames.ToArray(), ai.ModelSize, ai.ScoreThreshold, ai.ScoreNMS);
                foreach (string path in paths)
                using (var source = Cv2.ImRead(path))
                {
                    var rects = settings.RoiEnable
                        ? (List<Rect>)typeof(NodeTDAI).GetMethod("BuildRoiRects", BindingFlags.NonPublic | BindingFlags.Static)
                            .Invoke(null, new object[] { settings.RoiRegions, source.Width, source.Height })
                        : new List<Rect> { new Rect(0, 0, source.Width, source.Height) };
                    var boxes = new List<DetResult>();
                    foreach (var rect in rects)
                        using (var crop = new Mat(source, rect)) boxes.AddRange(detector.Detect(crop, rect.X, rect.Y));
                    samples.Add(new Sample { Path = path, Boxes = boxes });
                    Console.WriteLine("模型推理完成：" + System.IO.Path.GetFileName(path));
                }
            }
            finally { detector.Destroy(); }
            var results = new List<object>();
            foreach (int learningFrames in new[] { 1, 2 })
            {
                string key = "局部轮廓离线验证";
                var items = new[] { "前防水栓破损", "后防水栓破损" }.Select(n => new DetectItemInfo
                    { Name = n, Enable = true, MinValue = "95", MaxValue = "100" }).ToList();
                Solution.Instance.DetectItemDic[key] = items;
                var param = new NodeParamTDAI { CurDetectItemName = key, NodeName = key, IsAutoStudy = true,
                    StudyNum = learningFrames, StudyPercentage = 5 };
                var process = new TDJS_Vision.Process(key);
                var output = new NodeResultTDAI();
                foreach (var sample in samples.Take(learningFrames)) Run(sample, param, process, ref output);
                foreach (var sample in samples)
                {
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    Run(sample, param, process, ref output);
                    watch.Stop();
                    results.Add(new { LearningFrames = learningFrames, Image = System.IO.Path.GetFileName(sample.Path),
                        Milliseconds = watch.Elapsed.TotalMilliseconds, sample.Boxes,
                        Items = items.Select(i => new { i.Name, i.MinValue, i.MaxValue, i.CurValue,
                            Regions = output.AlgorithmResult.DetectResults[i.Name], Templates = i.PlugColor.ShapeTemplates.Count }).ToArray(),
                        Diagnostics = output.AlgorithmResult.Texts });
                    Console.WriteLine(learningFrames + "帧学习 " + System.IO.Path.GetFileName(sample.Path) + " " +
                        string.Join("; ", items.Select(i => i.Name + "=" + i.CurValue + " " +
                            (output.AlgorithmResult.DetectResults[i.Name].All(r => r.IsOk) ? "OK" : "NG"))));
                }
                File.WriteAllText(System.IO.Path.Combine(args[2], "learned-" + learningFrames + ".items"), JsonConvert.SerializeObject(items, Formatting.Indented));
            }
            File.WriteAllText(System.IO.Path.Combine(args[2], "results.json"), JsonConvert.SerializeObject(results, Formatting.Indented));
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    /// <summary>将同一模型框和未修改的原图传入真实解析器。</summary>
    private static void Run(Sample sample, NodeParamTDAI param, TDJS_Vision.Process process, ref NodeResultTDAI output)
    {
        using (var source = Cv2.ImRead(sample.Path))
        using (var input = new OutputImage { Bitmaps = new List<Mat> { source } })
            NewTerminal12Parse.Parse(sample.Boxes, sample.Boxes.Count, param, process, ref output, input);
    }
}
