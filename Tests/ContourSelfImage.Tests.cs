using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Newtonsoft.Json.Linq;
using OpenCvSharp;
using TDJS_Vision.Node._3_Detection.ContourMatch;

/// <summary>从真实方案读取建模原图，复现组合模板原图搜索；不改写方案。</summary>
internal static class ContourSelfImageTests
{
    /// <summary>使用明确参数类型读取方案，禁止执行方案中的类型元数据。</summary>
    private static int Main(string[] args)
    {
        try
        {
            var root = JObject.Parse(File.ReadAllText(args[0]));
            var token = root["ProcessInfos"].SelectMany(process => process["NodeInfos"])
                .First(node => (string)node["NodeType"] == "ContourMatch")["NodeParam"]
                .Children<JProperty>().Single().Value;
            var parameters = token.ToObject<NodeParamContourMatch>();
            var model = parameters.Templates.First(item => item.Enabled).Model;
            var sessionType = typeof(NativeShapeMatcher).Assembly
                .GetType("TDJS_Vision.Node._3_Detection.ContourMatch.ContourMatchSession");
            var restore = sessionType.GetMethod("Restore", BindingFlags.Static | BindingFlags.NonPublic);
            var watch = Stopwatch.StartNew();
            using (var matcher = (IShapeMatcher)restore.Invoke(null, new object[] { model, null }))
            using (var image = Cv2.ImDecode(model.ModelImageBytes, ImreadModes.Color))
            {
                Console.WriteLine("图像={0}x{1} ROI={2} 特征={3} 建模={4}ms", image.Width, image.Height,
                    model.ModelRoi, matcher.GetModelFeatures().Count, watch.ElapsedMilliseconds);
                // 使用方案原始参数验证生产会话，包括模板恢复、擦除重放、结果合并和缓存复用。
                using (var session = (IDisposable)Activator.CreateInstance(sessionType,
                    BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { null }, null))
                for (int run = 0; run < 2; run++)
                {
                    var execution = sessionType.GetMethod("Execute", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(session, new object[] { image, parameters, CancellationToken.None, null });
                    var hits = (IReadOnlyList<ShapeMatchResult>)execution.GetType()
                        .GetProperty("Matches", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(execution);
                    if (hits.Count != 1 || hits[0].Score < parameters.FindOptions.MinimumScore
                        || hits[0].TemplateId != parameters.Templates.First(item => item.Enabled).Id)
                        throw new Exception("方案原始参数的生产会话未找到正确模板。");
                    Console.WriteLine("生产会话 第{0}次 分数={1:F4}", run + 1, hits[0].Score);
                }
                foreach (int level in new[] { 0, 3, 2, 1 })
                foreach (int mode in new[] { 0, 1 })
                {
                    var options = parameters.FindOptions;
                    if (args.Length > 1 && bool.Parse(args[1])) options.AngleStartDegrees = options.AngleEndDegrees = 0;
                    options.PyramidLevels = level;
                    options.Mode = (ShapeMatchMode)mode;
                    watch.Restart();
                    var results = matcher.Find(image, options);
                    Console.WriteLine("模式={0} 层数={1} 耗时={2}ms 结果={3}", mode, level, watch.ElapsedMilliseconds,
                        string.Join("; ", results.Select(hit => string.Format("分数={0:F4} 中心=({1:F2},{2:F2}) 角度={3:F2}", hit.Score, hit.CenterX, hit.CenterY, hit.AngleDegrees))));
                    if (results.Count != 1 || results[0].Score < parameters.FindOptions.MinimumScore
                        || Math.Abs(results[0].CenterX - (model.ModelRoi.X + model.ModelRoi.Width * 0.5)) > 3
                        || Math.Abs(results[0].CenterY - (model.ModelRoi.Y + model.ModelRoi.Height * 0.5)) > 3
                        || Math.Abs(results[0].AngleDegrees) > 0.5)
                        throw new Exception("建模原图必须在原位置达到指定阈值。");
                }
                if (args.Length > 1 && bool.Parse(args[1])) return 0;
                var find = parameters.FindOptions;
                find.PyramidLevels = 0;
                find.Mode = ShapeMatchMode.快速;
                foreach (double angle in new[] { 0.0, 13.0, -7.0 })
                using (var transform = Cv2.GetRotationMatrix2D(new Point2f(
                    model.ModelRoi.X + model.ModelRoi.Width * 0.5f,
                    model.ModelRoi.Y + model.ModelRoi.Height * 0.5f), -angle, 1))
                using (var shifted = new Mat())
                {
                    transform.Set(0, 2, transform.At<double>(0, 2) + 300);
                    transform.Set(1, 2, transform.At<double>(1, 2) + 400);
                    Cv2.WarpAffine(image, shifted, transform, new Size(3600, 3200));
                    watch.Restart();
                    var hits = matcher.Find(shifted, find);
                    Console.WriteLine("平移(300,400) 旋转={0} 耗时={1}ms 结果={2}", angle, watch.ElapsedMilliseconds,
                        string.Join("; ", hits.Select(hit => string.Format("分数={0:F4} 中心=({1:F2},{2:F2}) 角度={3:F2}", hit.Score, hit.CenterX, hit.CenterY, hit.AngleDegrees))));
                    if (hits.Count != 1 || Math.Abs(hits[0].AngleDegrees - angle) > 0.5
                        || Math.Abs(hits[0].CenterX - (model.ModelRoi.X + model.ModelRoi.Width * 0.5 + 300)) > 3
                        || Math.Abs(hits[0].CenterY - (model.ModelRoi.Y + model.ModelRoi.Height * 0.5 + 400)) > 3)
                        throw new Exception("平移旋转后的模板必须在变换后的位置找到。");
                }
                using (var blank = new Mat(image.Size(), image.Type(), Scalar.All(0)))
                    if (matcher.Find(blank, find).Count != 0) throw new Exception("空白图像不应产生匹配。");
            }
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
