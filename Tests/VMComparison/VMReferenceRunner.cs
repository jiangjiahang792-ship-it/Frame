using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using VM.Core;
using ImageSourceModuleCs;
using IMVSContourMatchModuCs;
using VM.PlatformSDKCS;

/// <summary>通过已安装的官方 SDK 执行 VM 基准副本，输出逐图真实结果，不修改原图与方案。</summary>
internal static class VMReferenceRunner
{
    /// <summary>官方安装目录，用于解析已有依赖。</summary>
    private static string _installation;
    /// <summary>一次建立依赖索引，避免每帧扫描文件。</summary>
    private static Dictionary<string, string> _assemblies;
    /// <summary>读取属性和字段，保留官方结果结构以便检查比较口径。</summary>
    private static string Describe(object value, int depth = 0)
    {
        if (value == null) return "null";
        var type = value.GetType();
        if (type.IsPrimitive || type.IsEnum || value is string || value is decimal) return Convert.ToString(value, CultureInfo.InvariantCulture);
        if (depth > 3) return type.FullName;
        if (value is IEnumerable) return "[" + string.Join(",", ((IEnumerable)value).Cast<object>().Take(8).Select(item => Describe(item, depth + 1))) + "]";
        var members = new List<string>();
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            members.Add(field.Name + "=" + Describe(field.GetValue(value), depth + 1));
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(item => item.GetIndexParameters().Length == 0))
            try { members.Add(property.Name + "=" + Describe(property.GetValue(value), depth + 1)); } catch { }
        return type.Name + "{" + string.Join(",", members) + "}";
    }
    /// <summary>先安装依赖解析器再进入 SDK 调用，避免 JIT 提前解析第三方程序集失败。</summary>
    [STAThread]
    private static int Main(string[] args)
    {
        _installation = args[0];
        _assemblies = Directory.EnumerateFiles(_installation, "*.dll", SearchOption.AllDirectories)
            .Where(path => !path.Contains("\\Tools\\") && !path.Contains("\\x86\\"))
            .GroupBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) =>
        {
            string path;
            return _assemblies.TryGetValue(new AssemblyName(eventArgs.Name).Name, out path) ? Assembly.LoadFrom(path) : null;
        };
        try { int result = Execute(args); Environment.Exit(result); return result; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    /// <summary>载入用户在 VM 中另存的基准，并通过官方模块入口执行。</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static int Execute(string[] args)
    {
        VmSolution.Load(args[1], "", true);
        var procedure = (VmProcedure)VmSolution.Instance["流程1"];
        var source = (ImageSourceModuleTool)VmSolution.Instance["流程1.图像源1"];
        var matcher = (IMVSContourMatchModuTool)VmSolution.Instance["流程1.轮廓匹配1"];
        if (args.Length > 3 && args[3] == "ignore") {
            var polarity = matcher.ModuParams.GetType().GetProperty("Polarity");
            polarity.SetValue(matcher.ModuParams, Enum.Parse(polarity.PropertyType, "No"), null);
        }
        Console.WriteLine("VM 参数：" + Describe(matcher.ModuParams));
        Console.WriteLine("VM 图像源：" + Describe(source.ModuParams));
        source.ModuParams.AutoPlay = false;
        source.ModuParams.ImageSourceType = ImageSourceModuleCs.ImageSourceParam.ImageSourceTypeEnum.SDK;
        var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        var samples = Directory.Exists(args[2])
            ? serializer.Deserialize<List<Dictionary<string, object>>>(File.ReadAllText(Path.Combine(args[2], "manifest.json")))
            : new List<Dictionary<string, object>> { new Dictionary<string, object> { { "Id", "probe" }, { "InputPath", args[2] }, { "Kind", "探测" } } };
        var records = new List<object>();
        foreach (var sample in samples)
        using (var bitmap = new System.Drawing.Bitmap((string)sample["InputPath"]))
        using (var converted = new System.Drawing.Bitmap(bitmap.Width, bitmap.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb))
        {
        using (var graphics = System.Drawing.Graphics.FromImage(converted)) graphics.DrawImageUnscaled(bitmap, 0, 0);
        var data = converted.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
            System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        var gray = new byte[bitmap.Width * bitmap.Height];
        var pixels = new byte[data.Stride * bitmap.Height];
        Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
        converted.UnlockBits(data);
        for (int y = 0; y < bitmap.Height; y++)
        for (int x = 0; x < bitmap.Width; x++) gray[y * bitmap.Width + x] = pixels[y * data.Stride + x * 3];
        var input = new ImageBaseData(gray, (uint)gray.Length, bitmap.Width, bitmap.Height, VMPixelFormat.VM_PIXEL_MONO_08);
        source.SetImageData(input, true);
        procedure.Run(false);
        var algorithms = new List<double>(); var modules = new List<double>(); var walls = new List<double>();
        for (int repeat = 0; repeat < 5; repeat++)
        {
            // SDK 图像输入由流程逐次消费；每次触发都重新交付同一帧，禁止把输入错误计为漏检。
            source.SetImageData(input, true);
            var watch = Stopwatch.StartNew(); procedure.Run(false); watch.Stop();
            if (matcher.ModuResult.ErrorCode != 0)
                throw new InvalidOperationException("VM 运行异常：" + matcher.ModuResult.ErrorCode + " 图像源=" + Describe(source.ModuResult));
            algorithms.Add(Convert.ToDouble(matcher.AlgorithmTime)); modules.Add(Convert.ToDouble(matcher.ModuleTime)); walls.Add(watch.Elapsed.TotalMilliseconds);
        }
        var result = matcher.ModuResult;
        var hits = new List<object>();
        for (int index = 0; index < result.MatchNum; index++) hits.Add(new {
            CenterX = result.MatchRect[index].CenterPoint.X, CenterY = result.MatchRect[index].CenterPoint.Y,
            AngleDegrees = result.MatchRect[index].Angle, Score = result.MatchScore[index],
            AnchorX = result.MatchPoint[index].X, AnchorY = result.MatchPoint[index].Y });
        var row = new { Id = sample["Id"], Kind = sample["Kind"], Count = result.MatchNum, Hits = hits,
            AlgorithmMs = algorithms, ModuleMs = modules, WallMs = walls, MedianMs = algorithms.OrderBy(value => value).ElementAt(2), result.ErrorCode };
        records.Add(row);
        Console.WriteLine("{0} 数量={1} 算法={2:F3}ms 错误={3}", sample["Id"], result.MatchNum, row.MedianMs, result.ErrorCode);
        if (Directory.Exists(args[2])) File.WriteAllText(Path.Combine(args[2], args.Length > 4 ? args[4] : "vm-reference.json"), serializer.Serialize(records));
        GC.KeepAlive(input);
        }
        Console.WriteLine("VM_REFERENCE_COMPLETE");
        VmSolution.Instance.Dispose();
        return 0;
    }
}
