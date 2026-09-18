using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenCvSharp;
using TDJS_Vision.Node._3_Detection.ContourMatch;

/// <summary>统一灰度输入、真实图像与已知姿态基准，计时范围为已建模后的算法调用。</summary>
internal static class ContourDatasetRunner
{
    /// <summary>两套算法共享的输入清单。</summary>
    internal sealed class Sample
    {
        /// <summary>稳定编号。</summary>
        public string Id;
        /// <summary>原始图像路径。</summary>
        public string SourcePath;
        /// <summary>统一解码后的无损灰度文件。</summary>
        public string InputPath;
        /// <summary>真实图片、已知变换或无目标负例。</summary>
        public string Kind;
        /// <summary>已知目标中心 X。</summary>
        public double? ExpectedX;
        /// <summary>已知目标中心 Y。</summary>
        public double? ExpectedY;
        /// <summary>已知顺时针角度。</summary>
        public double? ExpectedAngle;
    }
    /// <summary>将同一无损灰度输入交给两套算法，解码时间不计入算法耗时。</summary>
    private static void Prepare(string solution, string imageDirectory, string directory)
    {
        Directory.CreateDirectory(Path.Combine(directory, "images"));
        var root = JObject.Parse(File.ReadAllText(solution));
        var token = root["ProcessInfos"].SelectMany(process => process["NodeInfos"])
            .First(node => (string)node["NodeType"] == "ContourMatch")["NodeParam"].Children<JProperty>().Single().Value;
        var parameters = token.ToObject<NodeParamContourMatch>();
        var model = parameters.Templates.First(item => item.Enabled).Model;
        // 对齐 VM 当前的全图搜索范围、最低分数、数量和极性，不缩小搜索区获得速度优势。
        model.ModelOptions.AngleStartDegrees = -45; model.ModelOptions.AngleEndDegrees = 45;
        parameters.FindOptions.AngleStartDegrees = -45; parameters.FindOptions.AngleEndDegrees = 45;
        parameters.FindOptions.MinimumScore = 0.5; parameters.FindOptions.MaximumMatches = 1;
        parameters.AllSearch = true;
        File.WriteAllText(Path.Combine(directory, "parameters.json"), JsonConvert.SerializeObject(parameters, Formatting.Indented));
        var samples = new List<Sample>();
        foreach (string path in Directory.EnumerateFiles(imageDirectory, "*.jpg", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal))
        {
            string id = "original-" + samples.Count.ToString("D3");
            string input = Path.Combine(directory, "images", id + ".png");
            using (var image = Cv2.ImRead(path, ImreadModes.Grayscale)) { if (image.Empty()) throw new Exception("读取失败：" + path); image.SaveImage(input); }
            samples.Add(new Sample { Id = id, SourcePath = path, InputPath = input, Kind = "原始图像" });
        }
        using (var original = Mat.FromImageData(model.ModelImageBytes, ImreadModes.Grayscale))
        {
            string input = Path.Combine(directory, "images", "template.png"); original.SaveImage(input);
            double centerX = model.ModelRoi.X + model.ModelRoi.Width * 0.5;
            double centerY = model.ModelRoi.Y + model.ModelRoi.Height * 0.5;
            samples.Add(new Sample { Id = "template", InputPath = input, Kind = "建模原图", ExpectedX = centerX, ExpectedY = centerY, ExpectedAngle = 0 });
            int number = 0;
            foreach (double angle in new[] { -30.0, -13.0, -7.0, -1.3, -0.5, -0.1, 0.0, 0.1, 0.5, 1.3, 7.0, 13.0, 30.0 })
            {
                double x = 1800.25 + (number % 3) * 17.2, y = 1500.4 + (number % 4) * 11.3;
                using (var matrix = Cv2.GetRotationMatrix2D(new Point2f((float)centerX, (float)centerY), -angle, 1))
                using (var image = new Mat())
                {
                    matrix.Set(0, 2, matrix.At<double>(0, 2) + x - centerX);
                    matrix.Set(1, 2, matrix.At<double>(1, 2) + y - centerY);
                    Cv2.WarpAffine(original, image, matrix, new OpenCvSharp.Size(4096, 3072), InterpolationFlags.Linear, BorderTypes.Constant, Scalar.All(0));
                    string id = "pose-" + number++.ToString("D2");
                    string transformed = Path.Combine(directory, "images", id + ".png"); image.SaveImage(transformed);
                    samples.Add(new Sample { Id = id, InputPath = transformed, Kind = "已知刚体变换", ExpectedX = x, ExpectedY = y, ExpectedAngle = angle });
                }
            }
            using (var blank = new Mat(original.Size(), original.Type(), Scalar.All(0)))
            {
                string path = Path.Combine(directory, "images", "blank.png"); blank.SaveImage(path);
                samples.Add(new Sample { Id = "blank", InputPath = path, Kind = "无目标" });
            }
        }
        File.WriteAllText(Path.Combine(directory, "manifest.json"), JsonConvert.SerializeObject(samples, Formatting.Indented));
        Console.WriteLine("统一输入完成：" + samples.Count);
    }
    /// <summary>运行单种模式，保留每次计时与结果，冷启动不混入稳态比较。</summary>
    private static void Run(string directory, string output, int mode, int repeats)
    {
        var samples = JsonConvert.DeserializeObject<List<Sample>>(File.ReadAllText(Path.Combine(directory, "manifest.json")));
        var parameters = JsonConvert.DeserializeObject<NodeParamContourMatch>(File.ReadAllText(Path.Combine(directory, "parameters.json")));
        parameters.FindOptions.Mode = (ShapeMatchMode)mode;
        var model = parameters.Templates.First(item => item.Enabled).Model;
        var restore = typeof(NativeShapeMatcher).Assembly.GetType("TDJS_Vision.Node._3_Detection.ContourMatch.ContourMatchSession")
            .GetMethod("Restore", BindingFlags.Static | BindingFlags.NonPublic);
        var results = new List<object>();
        using (var matcher = (IShapeMatcher)restore.Invoke(null, new object[] { model, null }))
        {
            foreach (var sample in samples)
            using (var image = Cv2.ImRead(sample.InputPath, ImreadModes.Grayscale))
            {
                matcher.Find(image, parameters.FindOptions);
                var milliseconds = new List<double>();
                IReadOnlyList<ShapeMatchResult> hits = null;
                for (int repeat = 0; repeat < repeats; repeat++)
                {
                    var timer = Stopwatch.StartNew(); hits = matcher.Find(image, parameters.FindOptions); timer.Stop();
                    milliseconds.Add(timer.Elapsed.TotalMilliseconds);
                }
                var row = new { sample.Id, sample.Kind, Mode = mode, Count = hits.Count, Hits = hits, Milliseconds = milliseconds,
                    MedianMs = milliseconds.OrderBy(value => value).ElementAt(milliseconds.Count / 2) };
                results.Add(row);
                Console.WriteLine("{0} 数量={1} 中位耗时={2:F2}ms {3}", sample.Id, hits.Count, row.MedianMs,
                    hits.Count == 0 ? "" : string.Format("X={0:F3} Y={1:F3} A={2:F4} S={3:F4}", hits[0].CenterX, hits[0].CenterY, hits[0].AngleDegrees, hits[0].Score));
                File.WriteAllText(output, JsonConvert.SerializeObject(results, Formatting.Indented));
            }
        }
    }
    /// <summary>固定随机种子追加与现场数据独立的无目标压力样本，并导出模型轮廓供离线复核。</summary>
    private static void AddControls(string directory)
    {
        var path = Path.Combine(directory, "manifest.json");
        var samples = JsonConvert.DeserializeObject<List<Sample>>(File.ReadAllText(path));
        samples.RemoveAll(item => item.Id.StartsWith("negative-", StringComparison.Ordinal));
        var parameters = JsonConvert.DeserializeObject<NodeParamContourMatch>(File.ReadAllText(Path.Combine(directory, "parameters.json")));
        var model = parameters.Templates.First(item => item.Enabled).Model;
        Action<string, Mat> save = (id, mat) => {
            string input = Path.Combine(directory, "images", id + ".png"); mat.SaveImage(input);
            samples.Add(new Sample { Id = id, InputPath = input, Kind = "无目标" });
        };
        using (var original = Mat.FromImageData(model.ModelImageBytes, ImreadModes.Grayscale))
        {
            foreach (int value in new[] { 64, 128, 255 })
            using (var uniform = new Mat(original.Size(), MatType.CV_8UC1, Scalar.All(value))) save("negative-uniform-" + value, uniform);
            using (var mirror = new Mat()) { Cv2.Flip(original, mirror, FlipMode.Y); save("negative-mirror", mirror); }
            var random = new Random(190918);
            for (int index = 0; index < 6; ++index)
            using (var noise = new Mat(original.Size(), MatType.CV_8UC1))
            {
                var bytes = new byte[original.Rows * original.Cols]; random.NextBytes(bytes);
                System.Runtime.InteropServices.Marshal.Copy(bytes, 0, noise.Data, bytes.Length);
                if (index > 0) Cv2.GaussianBlur(noise, noise, new OpenCvSharp.Size(index * 2 + 1, index * 2 + 1), 0);
                save("negative-noise-" + index, noise);
            }
            for (int index = 0; index < 6; ++index)
            using (var clutter = new Mat(original.Size(), MatType.CV_8UC1, Scalar.All(20)))
            {
                for (int item = 0; item < 75; ++item)
                {
                    int x = random.Next(20, original.Cols - 250), y = random.Next(20, original.Rows - 250);
                    var color = Scalar.All(random.Next(50, 256));
                    if (item % 2 == 0) Cv2.Circle(clutter, new OpenCvSharp.Point(x, y), random.Next(15, 150), color, random.Next(2, 12));
                    else Cv2.Rectangle(clutter, new Rect(x, y, random.Next(20, 220), random.Next(20, 220)), color, random.Next(2, 10));
                }
                save("negative-clutter-" + index, clutter);
            }
        }
        var restore = typeof(NativeShapeMatcher).Assembly.GetType("TDJS_Vision.Node._3_Detection.ContourMatch.ContourMatchSession")
            .GetMethod("Restore", BindingFlags.Static | BindingFlags.NonPublic);
        using (var matcher = (IShapeMatcher)restore.Invoke(null, new object[] { model, null }))
            File.WriteAllText(Path.Combine(directory, "model-contours.json"), JsonConvert.SerializeObject(matcher.GetModelContours()));
        File.WriteAllText(path, JsonConvert.SerializeObject(samples, Formatting.Indented));
        Console.WriteLine("输入总数：" + samples.Count);
    }
    /// <summary>明确选择准备输入或执行基准；不会连接设备。</summary>
    private static int Main(string[] args)
    {
        try
        {
            if (args[0] == "prepare") Prepare(args[1], args[2], Path.GetFullPath(args[3]));
            else if (args[0] == "controls") AddControls(Path.GetFullPath(args[1]));
            else Run(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), int.Parse(args[3]), int.Parse(args[4]));
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
