using System;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace TDJS_Vision.Properties
{
    /// <summary>使用可恢复的原子文件存储，避免默认提供器解析坏配置时令启动失败。</summary>
    [SettingsProvider(typeof(RecoverableSettingsProvider))]
    internal sealed partial class Settings { }

    /// <summary>兼容原user.config结构的用户设置提供器；不依赖应用日志初始化。</summary>
    public sealed class RecoverableSettingsProvider : SettingsProvider
    {
        /// <summary>同进程全部设置读写串行执行，避免保存与恢复互相覆盖。</summary>
        private static readonly object Sync = new object();
        /// <summary>本提供器对应的配置文件存储。</summary>
        private RecoverableSettingsFile _file;
        /// <summary>设置所属应用名称。</summary>
        public override string ApplicationName { get; set; } = "TDJS_Vision";

        /// <summary>初始化时保留原.NET用户配置路径，包括路径损坏时异常携带的文件名。</summary>
        public override void Initialize(string name, NameValueCollection config)
        {
            base.Initialize(name ?? nameof(RecoverableSettingsProvider), config ?? new NameValueCollection());
            string path = null;
            try { path = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath; }
            catch (ConfigurationErrorsException ex)
            {
                for (Exception current = ex; current != null; current = current.InnerException)
                {
                    var error = current as ConfigurationErrorsException;
                    if (!string.IsNullOrEmpty(error?.Filename) &&
                        string.Equals(Path.GetFileName(error.Filename), "user.config", StringComparison.OrdinalIgnoreCase))
                    { path = error.Filename; break; }
                }
                RecoverableSettingsFile.Report("定位原配置时发现异常：" + ex.Message);
            }
            if (string.IsNullOrEmpty(path))
                path = Path.Combine(System.Windows.Forms.Application.LocalUserAppDataPath, "user.config");
            _file = new RecoverableSettingsFile(path);
        }

        /// <summary>加载有效配置；文件或个别值无效时回退属性声明的默认值。</summary>
        public override SettingsPropertyValueCollection GetPropertyValues(SettingsContext context, SettingsPropertyCollection properties)
        {
            lock (Sync)
            {
                XDocument document = _file.Read();
                XElement section = document.Root.Element("userSettings")?.Element("TDJS_Vision.Properties.Settings");
                var values = new SettingsPropertyValueCollection();
                foreach (SettingsProperty property in properties)
                {
                    string text = null;
                    if (section != null)
                        foreach (XElement setting in section.Elements("setting"))
                            if ((string)setting.Attribute("name") == property.Name) { text = (string)setting.Element("value"); break; }
                    var value = new SettingsPropertyValue(property);
                    // 当前应用只定义字符串和布尔用户设置，显式转换避免损坏值静默变成错误状态。
                    bool flag;
                    if (text != null && (property.PropertyType != typeof(bool) || bool.TryParse(text, out flag)))
                        value.SerializedValue = text;
                    else
                    {
                        value.SerializedValue = property.DefaultValue;
                        if (text != null) RecoverableSettingsFile.Report("配置值无效，使用默认值：" + property.Name);
                    }
                    value.IsDirty = false;
                    values.Add(value);
                }
                return values;
            }
        }

        /// <summary>全部保存调用统一经过备份和原子替换；磁盘错误保留原文件，不使软件崩溃。</summary>
        public override void SetPropertyValues(SettingsContext context, SettingsPropertyValueCollection values)
        {
            lock (Sync)
            {
                try
                {
                    XDocument document = _file.Read();
                    XElement user = document.Root.Element("userSettings");
                    if (user == null) { user = new XElement("userSettings"); document.Root.Add(user); }
                    XElement section = user.Element("TDJS_Vision.Properties.Settings");
                    if (section == null) { section = new XElement("TDJS_Vision.Properties.Settings"); user.Add(section); }
                    foreach (SettingsPropertyValue value in values)
                    {
                        XElement existing = null;
                        foreach (XElement setting in section.Elements("setting"))
                            if ((string)setting.Attribute("name") == value.Name) { existing = setting; break; }
                        var replacement = new XElement("setting", new XAttribute("name", value.Name),
                            new XAttribute("serializeAs", "String"), new XElement("value", value.SerializedValue ?? ""));
                        if (existing == null) section.Add(replacement); else existing.ReplaceWith(replacement);
                    }
                    _file.Write(document);
                    foreach (SettingsPropertyValue value in values) value.IsDirty = false;
                }
                catch (Exception ex) when (RecoverableSettingsFile.IsFileError(ex))
                { RecoverableSettingsFile.Report("保存用户设置失败，原配置/备份保留：" + ex.Message); }
            }
        }
    }

    /// <summary>负责XML校验、坏文件留存、备份恢复和同卷原子替换。</summary>
    internal sealed class RecoverableSettingsFile
    {
        /// <summary>当前用户配置完整路径。</summary>
        private readonly string _path;
        /// <summary>创建指定路径的配置存储，便于离线故障回归。</summary>
        internal RecoverableSettingsFile(string path) { _path = path; }
        /// <summary>创建兼容原用户设置结构的空配置，属性默认值由Settings声明提供。</summary>
        private static XDocument Empty() => new XDocument(new XElement("configuration"));
        /// <summary>禁用外部实体并验证根节点，拒绝零字节、截断等坏XML。</summary>
        private static XDocument Load(string path)
        {
            using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
            {
                XDocument document = XDocument.Load(reader);
                if (document.Root?.Name != "configuration") throw new XmlException("用户配置根节点无效。");
                return document;
            }
        }
        /// <summary>读取并恢复配置；恢复写盘失败也返回有效备份或默认值供本次启动使用。</summary>
        internal XDocument Read()
        {
            if (File.Exists(_path))
            {
                try { return Load(_path); }
                catch (Exception ex) when (IsFileError(ex))
                {
                    Report("用户配置损坏或不可读：" + _path + "；" + ex.Message);
                    try { File.Copy(_path, _path + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".bak"); }
                    catch (Exception copyError) when (IsFileError(copyError)) { Report("坏配置留存失败：" + copyError.Message); }
                }
            }
            XDocument recovery = null;
            if (File.Exists(_path + ".bak"))
            {
                try { recovery = Load(_path + ".bak"); }
                catch (Exception ex) when (IsFileError(ex)) { Report("配置备份无效：" + ex.Message); }
            }
            if (recovery == null) recovery = Empty();
            // 首次安装无需创建空文件；已有文件或备份时尝试修复，不用坏文件覆盖有效备份。
            if (File.Exists(_path) || File.Exists(_path + ".bak"))
            {
                try { Commit(recovery, null); Report("已恢复用户配置：" + _path); }
                catch (Exception ex) when (IsFileError(ex)) { Report("配置恢复无法写盘，本次使用内存设置：" + ex.Message); }
            }
            return recovery;
        }
        /// <summary>保存前确认旧文件有效，只将有效旧配置作为上一版备份。</summary>
        internal void Write(XDocument document)
        {
            Read();
            if (File.Exists(_path)) Load(_path);
            else Commit(document, _path + ".bak");
            Commit(document, _path + ".bak");
        }
        /// <summary>临时文件刷盘后原子替换；目标缺失时原子移动，不使用删除后复制。</summary>
        private void Commit(XDocument document, string backup)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            string temporary = _path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true, CloseOutput = false }))
                        document.Save(writer);
                    stream.Flush(true);
                }
                Load(temporary);
                if (File.Exists(_path)) File.Replace(temporary, _path, backup);
                else File.Move(temporary, _path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        /// <summary>仅隔离预期配置、文件访问错误，不吞掉编程错误。</summary>
        internal static bool IsFileError(Exception ex) => ex is IOException || ex is UnauthorizedAccessException || ex is XmlException || ex is System.Security.SecurityException;
        /// <summary>独立启动诊断，禁止调用依赖Settings的应用日志导致递归初始化。</summary>
        internal static void Report(string message)
        {
            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TDJS-Vision", "Diagnostics");
                Directory.CreateDirectory(folder);
                File.AppendAllText(Path.Combine(folder, "settings-recovery.log"), DateTime.Now.ToString("s") + " " + message + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception ex) when (IsFileError(ex)) { System.Diagnostics.Debug.WriteLine(message); }
        }
    }
}
