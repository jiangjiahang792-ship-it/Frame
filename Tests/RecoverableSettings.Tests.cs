using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Xml.Linq;

/// <summary>通过独立进程读取真实应用Settings，验证坏配置不会封死日志和启动设置。</summary>
internal static class RecoverableSettingsTests
{
    /// <summary>加载当前构建的正式程序集。</summary>
    private static readonly Assembly App = Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "机器视觉AI检测系统V1.0.exe"));
    /// <summary>测试入口；子进程具有独立于正式程序的用户配置目录。</summary>
    private static int Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            if (args.Length > 0) return Child(args);
            string path = Run("path").Trim();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            Run("save", "第一版&中文");
            Run("save", "第二版");
            Check(File.Exists(path + ".bak"), "保存生成备份");
            File.WriteAllText(path, "<configuration>\0截断");
            Check(Run("read").Trim() == "第一版&中文", "独立启动恢复上一版");
            Check(Directory.GetFiles(Path.GetDirectoryName(path), "user.config.corrupt-*.bak").Length > 0, "保留坏文件");
            XDocument.Load(path);
            File.WriteAllText(path, "");
            File.WriteAllText(path + ".bak", "<broken");
            Check(Run("read").Trim() == "", "配置和备份均损坏时默认启动");
            Run("save", "恢复后再保存");
            Check(Run("read").Trim() == "恢复后再保存", "恢复后正常持久化");
            File.Delete(path);
            Check(Run("read").Trim() == "", "主文件丢失恢复有效上一版");
            TestLockedFile();
            Console.WriteLine("通过：跨进程备份恢复、坏文件留存、零字节/截断/空字符、双损坏默认启动、再次保存、日志初始化。");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace); return 1; }
    }
    /// <summary>文件被其他程序占用时仍可从备份读取，写失败不得修改有效原文件。</summary>
    private static void TestLockedFile()
    {
        string folder = Path.Combine(Path.GetTempPath(), "TDJS-SettingsTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "user.config");
        const string original = "<configuration><marker>原文件</marker></configuration>";
        File.WriteAllText(path, original);
        File.WriteAllText(path + ".bak", "<configuration><marker>备份</marker></configuration>");
        Type type = App.GetType("TDJS_Vision.Properties.RecoverableSettingsFile", true);
        object storage = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { path }, null);
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var recovered = (XDocument)type.GetMethod("Read", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(storage, null);
            Check(recovered.Root.Element("marker").Value == "备份", "无法修复写盘时使用内存备份");
            bool failed = false;
            try { type.GetMethod("Write", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(storage, new object[] { XDocument.Parse("<configuration/>") }); }
            catch (TargetInvocationException ex) when (ex.InnerException is IOException) { failed = true; }
            Check(failed, "文件占用应报告保存失败");
        }
        Check(File.ReadAllText(path) == original, "失败保存不破坏原文件");
        Check(Directory.GetFiles(folder, "*.tmp-*").Length == 0, "失败保存不残留临时文件");
        File.Delete(path);
        File.Delete(path + ".bak");
        Directory.Delete(folder);
    }
    /// <summary>执行真实应用设置和日志初始化，而非测试替身。</summary>
    private static int Child(string[] args)
    {
        if (args[0] == "path") { Console.WriteLine(ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath); return 0; }
        Type log = App.GetType("Logger.LogLevelSettings", true);
        Type level = App.GetType("Logger.MsgLevel", true);
        Check((bool)log.GetMethod("IsEnabled").Invoke(null, new[] { Enum.Parse(level, "Info") }), "日志可初始化");
        Type settings = App.GetType("TDJS_Vision.Properties.Settings", true);
        var instance = (ApplicationSettingsBase)settings.GetProperty("Default").GetValue(null);
        if (args[0] == "save") { instance["SolutionAddress"] = args[1]; instance.Save(); }
        else Console.WriteLine(instance["SolutionAddress"]);
        return 0;
    }
    /// <summary>启动隔离子进程，确保每次都走首次Settings加载。</summary>
    private static string Run(params string[] args)
    {
        var start = new ProcessStartInfo(Assembly.GetExecutingAssembly().Location) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.StandardOutputEncoding = System.Text.Encoding.UTF8;
        start.Arguments = string.Join(" ", Array.ConvertAll(args, value => "\"" + value + "\""));
        using (var process = Process.Start(start))
        {
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Check(process.ExitCode == 0, "子进程失败：" + error);
            return output;
        }
    }
    /// <summary>断言失败时终止测试。</summary>
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
