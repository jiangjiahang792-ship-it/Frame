using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Logger;

namespace TDJS_Vision.Forms.SolutionBrowser
{
    /// <summary>方案目录服务，允许替换历史记录存储和方案发现策略。</summary>
    public interface ISolutionCatalog
    {
        /// <summary>获取历史记录和指定目录中的方案。</summary>
        IReadOnlyList<SolutionCatalogEntry> Read(IEnumerable<string> directories);
        /// <summary>记录已经成功保存或打开的方案。</summary>
        void Remember(string path);
        /// <summary>仅移除列表记录并持续忽略该路径，不删除对应的方案文件。</summary>
        void Forget(string path);
    }

    /// <summary>列表中的方案文件元数据，不加载设备、模型或方案内容。</summary>
    public sealed class SolutionCatalogEntry
    {
        /// <summary>方案名称。</summary>
        public string Name { get; set; }
        /// <summary>完整文件路径，用于区分同名方案。</summary>
        public string FullPath { get; set; }
        /// <summary>本地最后修改时间。</summary>
        public DateTime ModifiedAt { get; set; }
    }

    /// <summary>以当前用户本地文件保存历史，按文件路径去重并浅层发现旧方案。</summary>
    public sealed class FileSolutionCatalog : ISolutionCatalog
    {
        /// <summary>程序运行目录下的常用方案目录，不受进程当前工作目录影响。</summary>
        public static string DefaultDirectory { get; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ProjConfigs", "Sol");
        /// <summary>应用共用的目录服务。</summary>
        public static ISolutionCatalog Default { get; } = new FileSolutionCatalog(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TDJS-Vision", "方案历史.txt"));
        /// <summary>同一进程的历史读写锁。</summary>
        private static readonly object SyncRoot = new object();
        /// <summary>历史文件完整路径。</summary>
        private readonly string historyPath;
        /// <summary>用户主动删除的列表记录路径；独立保存可避免目录扫描再次显示。</summary>
        private readonly string ignoredPath;

        /// <summary>使用指定文件构建服务，便于隔离测试或更换存储位置。</summary>
        public FileSolutionCatalog(string historyPath)
        {
            this.historyPath = Path.GetFullPath(historyPath);
            ignoredPath = this.historyPath + ".忽略";
        }

        /// <summary>读取历史；失败时仍允许通过目录发现方案。</summary>
        private List<string> ReadHistory()
        {
            try { return File.Exists(historyPath) ? File.ReadAllLines(historyPath, Encoding.UTF8).ToList() : new List<string>(); }
            catch (Exception ex) { ReportFailure(ex); return new List<string>(); }
        }

        /// <summary>读取用户主动删除的记录；读取失败时按没有忽略项处理。</summary>
        private List<string> ReadIgnored()
        {
            try { return File.Exists(ignoredPath) ? File.ReadAllLines(ignoredPath, Encoding.UTF8).ToList() : new List<string>(); }
            catch (Exception ex) { ReportFailure(ex); return new List<string>(); }
        }

        /// <summary>原子写入路径记录，避免客户端异常退出留下半写文件。</summary>
        private static void WritePaths(string targetPath, IEnumerable<string> paths)
        {
            string temporaryPath = targetPath + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                File.WriteAllLines(temporaryPath, paths, new UTF8Encoding(false));
                if (File.Exists(targetPath)) File.Replace(temporaryPath, targetPath, null);
                else File.Move(temporaryPath, targetPath);
            }
            finally
            {
                try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
                catch (Exception ex) { ReportFailure(ex); }
            }
        }

        /// <summary>仅接收有效方案路径，排除内部空方案模板。</summary>
        private static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try
            {
                string fullPath = Path.GetFullPath(path);
                return string.Equals(Path.GetExtension(fullPath), ".Sol", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(Path.GetFileName(fullPath), "空方案.Sol", StringComparison.OrdinalIgnoreCase) ? fullPath : null;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException) { return null; }
        }

        /// <summary>原子更新历史；历史失败不能将成功保存的方案报告为保存失败。</summary>
        public void Remember(string path)
        {
            string fullPath = Normalize(path);
            if (fullPath == null || !File.Exists(fullPath)) return;
            lock (SyncRoot)
            {
                try
                {
                    var paths = ReadHistory().Select(Normalize).Where(p => p != null).ToList();
                    paths.RemoveAll(p => string.Equals(p, fullPath, StringComparison.OrdinalIgnoreCase));
                    paths.Insert(0, fullPath);
                    WritePaths(historyPath, paths.Distinct(StringComparer.OrdinalIgnoreCase));

                    var ignored = ReadIgnored().Select(Normalize).Where(p => p != null).ToList();
                    ignored.RemoveAll(p => string.Equals(p, fullPath, StringComparison.OrdinalIgnoreCase));
                    WritePaths(ignoredPath, ignored.Distinct(StringComparer.OrdinalIgnoreCase));
                }
                catch (Exception ex) { ReportFailure(ex); }
            }
        }

        /// <summary>删除列表记录并加入忽略集合，绝不删除方案文件。</summary>
        public void Forget(string path)
        {
            string fullPath = Normalize(path);
            if (fullPath == null) return;
            lock (SyncRoot)
            {
                try
                {
                    var history = ReadHistory().Select(Normalize).Where(p => p != null).ToList();
                    history.RemoveAll(p => string.Equals(p, fullPath, StringComparison.OrdinalIgnoreCase));
                    WritePaths(historyPath, history.Distinct(StringComparer.OrdinalIgnoreCase));

                    var ignored = ReadIgnored().Select(Normalize).Where(p => p != null).ToList();
                    ignored.RemoveAll(p => string.Equals(p, fullPath, StringComparison.OrdinalIgnoreCase));
                    ignored.Insert(0, fullPath);
                    WritePaths(ignoredPath, ignored.Distinct(StringComparer.OrdinalIgnoreCase));
                }
                catch (Exception ex) { ReportFailure(ex); }
            }
        }

        /// <summary>只读取文件元数据，不递归扫描，不反序列化方案。</summary>
        public IReadOnlyList<SolutionCatalogEntry> Read(IEnumerable<string> directories)
        {
            List<string> history;
            HashSet<string> ignored;
            lock (SyncRoot)
            {
                history = ReadHistory();
                ignored = new HashSet<string>(ReadIgnored().Select(Normalize).Where(p => p != null), StringComparer.OrdinalIgnoreCase);
                // 顺手清理已经不存在的历史记录，避免无效路径长期积累。
                var existingHistory = history.Select(Normalize).Where(p => p != null && File.Exists(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (existingHistory.Count != history.Count)
                {
                    try { WritePaths(historyPath, existingHistory); }
                    catch (Exception ex) { ReportFailure(ex); }
                }
                history = existingHistory;
            }
            var paths = new HashSet<string>(history.Where(p => !ignored.Contains(p)), StringComparer.OrdinalIgnoreCase);
            var folders = new HashSet<string>((directories ?? Enumerable.Empty<string>()).Where(p => !string.IsNullOrWhiteSpace(p)), StringComparer.OrdinalIgnoreCase);
            foreach (string path in paths) folders.Add(Path.GetDirectoryName(path));
            foreach (string folder in folders)
            {
                try
                {
                    foreach (string path in Directory.EnumerateFiles(folder, "*.Sol", SearchOption.TopDirectoryOnly))
                    {
                        string normalized = Normalize(path);
                        if (normalized != null && !ignored.Contains(normalized)) paths.Add(normalized);
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
                { ReportFailure(ex); }
            }
            var entries = new List<SolutionCatalogEntry>();
            foreach (string path in paths)
            {
                try
                {
                    var file = new FileInfo(path);
                    if (file.Exists) entries.Add(new SolutionCatalogEntry { Name = Path.GetFileNameWithoutExtension(path), FullPath = path, ModifiedAt = file.LastWriteTime });
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { ReportFailure(ex); }
            }
            return entries.OrderByDescending(p => p.ModifiedAt).ThenBy(p => p.Name).ToList();
        }

        /// <summary>记录目录问题，日志组件异常也不干扰方案操作。</summary>
        private static void ReportFailure(Exception ex)
        {
            try { LogHelper.AddLog(MsgLevel.Warn, "方案列表记录或读取失败：" + ex.Message, true); }
            catch { /* 目录为辅助功能，日志故障不影响主保存管线。 */ }
        }
    }
}
