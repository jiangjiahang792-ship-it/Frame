using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using TDJS_Vision;

/// <summary>不启动设备，验证正式方案保存与恢复入口的文件故障行为。</summary>
internal static class SolutionRecoveryTests
{
    /// <summary>调用正式程序集私有文件入口，避免测试替身绕过实际保存链路。</summary>
    private static object Call(string method, params object[] args)
    {
        try { return typeof(ConfigHelper).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args); }
        catch (TargetInvocationException ex) { throw ex.InnerException; }
    }
    /// <summary>创建带可识别参数的合法最小方案。</summary>
    private static string Json(string path, int interval) => new JObject { ["SolName"] = path, ["RunInterval"] = interval, ["Devices"] = new JObject(), ["ProcessInfos"] = new JArray() }.ToString();
    /// <summary>验证条件。</summary>
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    /// <summary>保存测试方案。</summary>
    private static void Save(string path, int interval) => Call("SafeWriteSolutionFile", path, Json(path, interval), "故障回归");
    /// <summary>读取并自动恢复测试方案。</summary>
    private static SolConfig Read(string path) => (SolConfig)Call("ReadSolutionWithRecovery", path);
    /// <summary>执行文件故障矩阵，只操作独立临时目录。</summary>
    private static int Main()
    {
        try
        {
            string folder = Path.Combine(Path.GetTempPath(), "TDJS-SolTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "客户方案.Sol");
            Save(path, 11);
            Check(File.Exists(path + ".bak"), "首次保存必须有备份");
            Save(path, 22);
            Check(Read(path).RunInterval == 22, "正常主文件优先");
            string backup = File.ReadAllText(path + ".bak");
            File.WriteAllText(path, "{\0截断");
            Check(Read(path).RunInterval == 11, "恢复上一版参数");
            Check(File.ReadAllText(path) == backup, "恢复不改检测参数文本");
            Check(Directory.GetFiles(folder, "*.corrupt-*").Length == 1, "保留损坏现场");
            File.Delete(path);
            Check(Read(path).RunInterval == 11, "丢失主文件恢复");
            using (var blocked = new FileStream(path + ".bak", FileMode.Open, FileAccess.Read, FileShare.None))
            {
                bool failed = false;
                try { Save(path, 33); } catch (IOException) { failed = true; }
                Check(failed, "备份不可替换应取消保存");
                Check(Read(path).RunInterval == 11, "备份失败保留主文件");
            }
            File.WriteAllText(path, "");
            bool rejected = false;
            try { Save(path, 44); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected && File.ReadAllText(path + ".bak") == backup, "坏主文件不能覆盖有效备份");
            File.WriteAllText(path + ".bak", "{}");
            bool noBackup = false;
            try { Read(path); } catch (InvalidDataException) { noBackup = true; }
            Check(noBackup && File.ReadAllText(path) == "", "无有效备份保留现场且拒绝空方案");
            string legacy = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Path.GetFileName(path) + ".bak-" + Guid.NewGuid().ToString("N"));
            try
            {
                File.WriteAllText(legacy, Json(Path.Combine(folder, "其他.Sol"), 55));
                bool mismatch = false;
                try { Read(path); } catch (InvalidDataException) { mismatch = true; }
                Check(mismatch, "旧备份完整路径不匹配不能恢复");
                File.WriteAllText(legacy, Json(path, 66));
                Check(Read(path).RunInterval == 66, "兼容程序目录旧备份");
            }
            finally { File.Delete(legacy); }
            Console.WriteLine("通过：首次备份、上一版恢复、原文保持、坏文件留存、主文件丢失、备份占用、坏文件保护、无有效备份拒绝、旧备份路径隔离及恢复。");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace); return 1; }
    }
}
