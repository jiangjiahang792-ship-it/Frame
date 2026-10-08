using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
#if PLACEMENT_IMPLEMENTED
using TDJS_Vision.Forms.RuntimeDesigner;
#endif

/// <summary>纯几何和临时配置文件测试，不加载窗口、生产方案或设备。</summary>
internal static class RuntimeWindowPlacementTests
{
    /// <summary>断言计数。</summary>
    private static int checks;
    /// <summary>验证条件并提供明确失败信息。</summary>
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
    /// <summary>验证损坏或越界数据被拒绝。</summary>
    private static void Reject(Action action, string message)
    {
        bool rejected = false;
        try { action(); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, message);
    }
    /// <summary>先验证契约存在，再执行独立行为测试。</summary>
    private static int Main()
    {
        try
        {
            Check(Assembly.GetExecutingAssembly().GetType("TDJS_Vision.Forms.RuntimeDesigner.RuntimeWindowPlacement") != null,
                "RED：缺少窗口位置模型实现");
#if PLACEMENT_IMPLEMENTED
            Geometry();
            Store();
#endif
            Console.WriteLine("RuntimeWindowPlacement: " + checks + " assertions passed.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
#if PLACEMENT_IMPLEMENTED
    /// <summary>创建有效屏幕模型。</summary>
    private static RuntimeDisplayInfo Display(string name, Rectangle area)
    { return new RuntimeDisplayInfo { DeviceName = name, WorkingArea = area }; }
    /// <summary>验证恢复位置完全可见，使用长整数避免测试自身溢出。</summary>
    private static void Visible(RuntimeWindowPlacement placement)
    {
        Rectangle area = placement.WorkingArea, bounds = placement.NormalBounds;
        Check(bounds.Width > 0 && bounds.Height > 0 && bounds.X >= area.X && bounds.Y >= area.Y &&
            (long)bounds.X + bounds.Width <= (long)area.X + area.Width &&
            (long)bounds.Y + bounds.Height <= (long)area.Y + area.Height, "窗口必须完整位于工作区");
    }
    /// <summary>验证原屏保持、相对缩放、负坐标、缺失屏幕及极端输入。</summary>
    private static void Geometry()
    {
        var left = Display("left", new Rectangle(-1920, -120, 1920, 1080));
        var main = Display("main", new Rectangle(0, 0, 1600, 900));
        var saved = new RuntimeWindowPlacement { DeviceName = "LEFT", WorkingArea = left.WorkingArea,
            NormalBounds = new Rectangle(-1720, -20, 1000, 600), Maximized = false };
        var same = RuntimeWindowPlacementGeometry.Resolve(saved, new[] { left, main }, "main", new Size(320, 240));
        Check(same.NormalBounds == saved.NormalBounds && !same.Maximized, "相同工作区保留原位置与状态");
        Check(!ReferenceEquals(saved, same) && !ReferenceEquals(saved, saved.Copy()), "输出及复制均为独立对象");
        var resized = Display("left", new Rectangle(-960, 100, 960, 540));
        var changed = RuntimeWindowPlacementGeometry.Resolve(saved, new[] { resized }, null, new Size(100, 100));
        Check(changed.NormalBounds == new Rectangle(-860, 150, 500, 300), "分辨率变化按工作区局部偏移和尺寸同比缩放");
        Visible(changed);
        var fallback = RuntimeWindowPlacementGeometry.Resolve(saved, new[] { main }, "main", new Size(320, 240));
        Check(fallback.DeviceName == "main", "缺失显示器使用宿主显示器"); Visible(fallback);
        var first = RuntimeWindowPlacementGeometry.Resolve(saved, new[] { main, resized }, "missing", Size.Empty);
        Check(first.DeviceName == "left", "已保存显示器优先于宿主回退");
        saved.DeviceName = "missing";
        first = RuntimeWindowPlacementGeometry.Resolve(saved, new[] { main, resized }, "missing", Size.Empty);
        Check(first.DeviceName == "main", "宿主也缺失时使用第一个有效显示器");
        var defaults = RuntimeWindowPlacementGeometry.Resolve(null, new[] { left, main }, "MAIN", new Size(320, 240));
        Check(defaults.Maximized && defaults.NormalBounds == new Rectangle(160, 90, 1280, 720), "首次默认最大化且普通窗口约八成居中");
        var largerMinimum = RuntimeWindowPlacementGeometry.Resolve(null, new[] { main }, null, new Size(1500, 850));
        Check(largerMinimum.NormalBounds == new Rectangle(50, 25, 1500, 850), "最小尺寸扩大默认窗口后仍需居中");
        var small = Display("small", new Rectangle(-100, -100, 80, 60));
        var capped = RuntimeWindowPlacementGeometry.Resolve(saved, new[] { small }, null, new Size(1000, 1000));
        Check(capped.NormalBounds == small.WorkingArea, "最小尺寸封顶为工作区"); Visible(capped);
        saved.NormalBounds = new Rectangle(int.MaxValue, int.MinValue, int.MaxValue, -1);
        var malformed = RuntimeWindowPlacementGeometry.Resolve(saved, new[] { main }, null, new Size(-1, int.MaxValue));
        Visible(malformed);
        saved.NormalBounds = new Rectangle(int.MinValue, int.MinValue, 100, 100);
        saved.WorkingArea = new Rectangle(int.MinValue, int.MinValue, 1, 1);
        var extreme = RuntimeWindowPlacementGeometry.Resolve(saved, new[] { main }, null, Size.Empty);
        Visible(extreme);
        var invalidDisplay = Display("invalid", new Rectangle(int.MaxValue, 0, 100, 100));
        var safe = RuntimeWindowPlacementGeometry.Resolve(null, new[] { invalidDisplay, left }, null, Size.Empty);
        Check(safe.DeviceName == "left", "无效溢出屏幕被排除"); Visible(safe);
    }
    /// <summary>验证路径隔离、原子读改写、坏文件保留及有界解析。</summary>
    private static void Store()
    {
        string directory = Path.Combine(Path.GetTempPath(), "TDJS-Placement-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string file = Path.Combine(directory, "settings.json");
            string solution = Path.Combine(directory, "A.Sol");
            var store = new JsonRuntimeWindowPlacementStore(file);
            var second = new JsonRuntimeWindowPlacementStore(file);
            var placement = new RuntimeWindowPlacement { DeviceName = "left", WorkingArea = new Rectangle(-1920, 0, 1920, 1080),
                NormalBounds = new Rectangle(-1800, 100, 900, 600), Maximized = true };
            Check(store.FilePath == Path.GetFullPath(file), "公开规范化存储路径");
            Check(store.Load(solution, "one") == null, "文件不存在返回空");
            store.Save(" ", null, null);
            Check(store.Load(null, null) == null && !File.Exists(file), "未保存方案不创建本机记录");
            store.Save(solution, "one", placement);
            placement.Maximized = false;
            var loaded = store.Load(Path.Combine(directory, ".", "a.sol"), "one");
            Check(loaded.Maximized && loaded.NormalBounds == placement.NormalBounds, "路径大小写和点段规范化且保存快照隔离");
            loaded.NormalBounds = Rectangle.Empty;
            Check(store.Load(solution, "one").NormalBounds == placement.NormalBounds, "加载对象不共享可变状态");
            second.Save(solution, "two", placement);
            second.Save(Path.Combine(directory, "B.Sol"), "one", placement);
            Check(store.Load(solution, "one").Maximized && !store.Load(solution, "two").Maximized,
                "重复存储实例保存保留其它页面");
            Check(!store.Load(Path.Combine(directory, "B.Sol"), "one").Maximized, "复制方案使用独立路径记录");
            Check(store.Load(solution, "ONE") == null, "稳定页面编号按精确值匹配");
            Parallel.For(0, 20, index => new JsonRuntimeWindowPlacementStore(file).Save(solution, "page" + index, placement));
            Check(Enumerable.Range(0, 20).All(index => store.Load(solution, "page" + index) != null), "并发存储实例不丢其它窗口记录");
            Check(Directory.GetFiles(directory, "*.tmp").Length == 0, "原子保存清理唯一临时文件");
            string valid = File.ReadAllText(file);
            Action<JObject, string> bad = (document, message) =>
            {
                string corrupt = document.ToString();
                File.WriteAllText(file, corrupt);
                Reject(() => store.Load(solution, "one"), message);
                Reject(() => store.Save(solution, "new", placement), message + "禁止覆盖");
                Check(File.ReadAllText(file) == corrupt, "坏配置保持原样");
            };
            var json = JObject.Parse(valid); json["Version"] = 2; bad(json, "未知版本拒绝");
            json = JObject.Parse(valid); json["$type"] = "System.Object"; bad(json, "类型元数据拒绝");
            json = JObject.Parse(valid); json["Records"][0]["Placement"]["NormalBounds"]["Width"] = 0; bad(json, "非法尺寸拒绝");
            json = JObject.Parse(valid); json["Records"][0]["Placement"]["WorkingArea"]["X"] = int.MaxValue; bad(json, "溢出坐标拒绝");
            json = JObject.Parse(valid); json["Records"][0]["Placement"]["Maximized"] = "true"; bad(json, "隐式类型转换拒绝");
            json = JObject.Parse(valid); json["Records"][0]["Placement"]["NormalBounds"]["X"] = long.MaxValue; bad(json, "超范围整数拒绝");
            json = JObject.Parse(valid); json["Records"][0]["PageId"] = " "; bad(json, "空页面编号拒绝");
            json = JObject.Parse(valid); ((JArray)json["Records"]).Add(json["Records"][0].DeepClone()); bad(json, "重复键拒绝");
            File.WriteAllText(file, "{\"Version\":1,\"Version\":1,\"Records\":[]}");
            Reject(() => store.Load(solution, "one"), "重复JSON属性拒绝");
            File.WriteAllText(file, "{"); Reject(() => store.Load(solution, "one"), "损坏JSON拒绝");
            File.WriteAllText(file, new string('[', 20) + new string(']', 20));
            Reject(() => store.Load(solution, "one"), "深层嵌套拒绝");
            File.WriteAllText(file, new string(' ', 2 * 1024 * 1024 + 1));
            Reject(() => store.Load(solution, "one"), "超大文件拒绝");
            File.WriteAllText(file, valid);
            placement.NormalBounds = Rectangle.Empty;
            Reject(() => store.Save(solution, "one", placement), "无效输入不能保存");
            Check(File.ReadAllText(file) == valid, "无效保存保留原文件");
        }
        finally
        {
            // 仅删除本测试创建的随机临时目录，绝不访问用户本机配置目录。
            string full = Path.GetFullPath(directory);
            string temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (full.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(full).StartsWith("TDJS-Placement-", StringComparison.Ordinal))
                Directory.Delete(full, true);
        }
    }
#endif
}
