using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using OpenCvSharp;
using TDJS_Vision;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._3_Detection.TDAI.Parse;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

/// <summary>对用户原图及AI合成破损图运行真实解析器；人工固定ROI代替YOLO，仅验证颜色周长链路。</summary>
internal static class PlugColorSamples
{
    /// <summary>按上前、上后、下前、下后固定的原图像素区域；前指右侧光滑段，后指左侧波纹段。</summary>
    private static readonly List<DetResult> Boxes = new List<DetResult>
    {
        new DetResult { ClassId = 5, Box = new Rect(445, 185, 220, 265) },
        new DetResult { ClassId = 6, Box = new Rect(180, 120, 265, 390) },
        new DetResult { ClassId = 5, Box = new Rect(420, 610, 225, 270) },
        new DetResult { ClassId = 6, Box = new Rect(155, 560, 265, 400) }
    };

    /// <summary>生成独立检测项，避免修改现场配置。</summary>
    private static DetectItemInfo Item(string name)
    {
        return new DetectItemInfo { Name = name, Enable = true, IsCountItem = false, MinValue = "0", MaxValue = "99999" };
    }

    /// <summary>运行解析器并输出结果、颜色参数和可视化；所有结果不预设应为NG。</summary>
    [STAThread]
    private static int Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            string directory = Path.GetFullPath(args[0]);
            var summaries = new List<object>();
            foreach (int tolerance in new[] { 5, 10 })
            {
                string key = "防水栓样图离线测试";
                var items = new List<DetectItemInfo> { Item("前防水栓破损"), Item("后防水栓破损") };
                Solution.Instance.DetectItemDic[key] = items;
                var param = new NodeParamTDAI { CurDetectItemName = key, NodeName = key, IsAutoStudy = true,
                    StudyNum = 1, StudyPercentage = tolerance, NeedConvert = false };
                var process = new TDJS_Vision.Process(key);
                var result = new NodeResultTDAI();
                using (var original = Cv2.ImRead(Path.Combine(directory, "original.jpg")))
                using (var input = new OutputImage { Bitmaps = new List<Mat> { original } })
                    NewTerminal12Parse.Parse(Boxes, Boxes.Count, param, process, ref result, input);
                File.WriteAllText(Path.Combine(directory, "learned-" + tolerance + ".items"), JsonConvert.SerializeObject(items, Formatting.Indented));
                foreach (string name in new[] { "original.jpg", "small-notch.png", "large-tear.png", "surface-crack.png" })
                {
                    using (var source = Cv2.ImRead(Path.Combine(directory, name)))
                    using (var image = new Mat())
                    {
                        // 生成器改变了分辨率，按原图1280×1024归一化，禁止直接比较不同像素尺度周长。
                        Cv2.Resize(source, image, new Size(1280, 1024), 0, 0, InterpolationFlags.Area);
                        using (var input = new OutputImage { Bitmaps = new List<Mat> { image } })
                            NewTerminal12Parse.Parse(Boxes, Boxes.Count, param, process, ref result, input);
                        summaries.Add(new { Image = name, OriginalWidth = source.Width, OriginalHeight = source.Height,
                            Tolerance = tolerance, OverallOk = result.IsOk,
                            Items = items.Select(i => new { i.Name, i.MinValue, i.MaxValue, i.PlugColor,
                                Regions = result.AlgorithmResult.DetectResults[i.Name].Select((r, index) =>
                                    new { Index = index, r.Value, r.IsOk }).ToArray() }).ToArray() });
                        using (var overlay = image.Clone())
                        {
                            foreach (var box in Boxes) Cv2.Rectangle(overlay, box.Box, new Scalar(255, 160, 0), 1);
                            foreach (var contour in result.AlgorithmResult.Contours)
                            {
                                var points = contour.Points.Select(p => new Point((int)p.X, (int)p.Y)).ToArray();
                                Cv2.Polylines(overlay, new[] { points }, true,
                                    new Scalar(contour.Color.B, contour.Color.G, contour.Color.R), 2);
                            }
                            int line = 30;
                            foreach (var pair in result.AlgorithmResult.DetectResults)
                            {
                                string text = (pair.Key.StartsWith("前") ? "Front: " : "Rear: ") +
                                    string.Join(" / ", pair.Value.Select(r => r.Value + (r.IsOk ? " OK" : " NG")));
                                Cv2.PutText(overlay, text, new Point(10, line), HersheyFonts.HersheySimplex, .65,
                                    pair.Value.All(r => r.IsOk) ? new Scalar(0, 255, 0) : new Scalar(0, 0, 255), 2);
                                line += 28;
                            }
                            Cv2.ImWrite(Path.Combine(directory, Path.GetFileNameWithoutExtension(name) + "-overlay-" + tolerance + ".png"), overlay);
                        }
                    }
                }
            }
            File.WriteAllText(Path.Combine(directory, "results.json"), JsonConvert.SerializeObject(new { Boxes, Results = summaries }, Formatting.Indented));
            Console.WriteLine("已完成3张合成破损图、原图对照及5%/10%公差测试：" + directory);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
