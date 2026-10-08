using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>本机窗口位置存储契约，与方案和模板序列化隔离。</summary>
    public interface IRuntimeWindowPlacementStore
    {
        /// <summary>按方案完整路径及稳定页面编号读取快照，未保存方案或无记录返回空。</summary>
        RuntimeWindowPlacement Load(string solutionPath, string pageId);
        /// <summary>仅更新指定页面的位置，未保存方案不持久化。</summary>
        void Save(string solutionPath, string pageId, RuntimeWindowPlacement placement);
    }

    /// <summary>有界本机JSON存储，多个实例共用设置锁并采用同目录原子替换。</summary>
    public sealed class JsonRuntimeWindowPlacementStore : IRuntimeWindowPlacementStore
    {
        /// <summary>只保护本进程设置文件读改写，不参与任何生产执行或通信。</summary>
        private static readonly object FileSync = new object();
        /// <summary>配置文件最大字节数。</summary>
        private const int MaximumBytes = 2 * 1024 * 1024;
        /// <summary>整个配置允许的最大记录数。</summary>
        private const int MaximumRecords = 2048;
        /// <summary>严格UTF8编码，拒绝无效字节并且不写字节序标记。</summary>
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        /// <summary>实际使用的规范化配置文件路径。</summary>
        public string FilePath { get; }

        /// <summary>指定本机配置路径，构造时不创建目录或读取文件。</summary>
        public JsonRuntimeWindowPlacementStore(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("窗口位置配置路径不能为空。", nameof(filePath));
            FilePath = Path.GetFullPath(filePath);
        }

        /// <summary>创建当前用户本机目录的存储实例，不访问方案目录。</summary>
        public static JsonRuntimeWindowPlacementStore CreateDefault()
        {
            return new JsonRuntimeWindowPlacementStore(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TDJS-Vision", "runtime-window-placements.json"));
        }

        /// <summary>读取独立位置快照，损坏文件抛出异常供宿主记录并回退。</summary>
        public RuntimeWindowPlacement Load(string solutionPath, string pageId)
        {
            if (string.IsNullOrWhiteSpace(solutionPath)) return null;
            string path = NormalizeSolutionPath(solutionPath);
            ValidateText(pageId, 128, "页面编号");
            lock (FileSync)
            {
                Record record = ReadRecords().FirstOrDefault(item => SameKey(item, path, pageId));
                return record?.Placement.Copy();
            }
        }

        /// <summary>先完整验证旧文件再更新单条记录，损坏配置绝不静默覆盖。</summary>
        public void Save(string solutionPath, string pageId, RuntimeWindowPlacement placement)
        {
            if (string.IsNullOrWhiteSpace(solutionPath)) return;
            string path = NormalizeSolutionPath(solutionPath);
            ValidateText(pageId, 128, "页面编号");
            RuntimeWindowPlacement snapshot = placement?.Copy();
            ValidatePlacement(snapshot);
            lock (FileSync)
            {
                List<Record> records = ReadRecords();
                Record record = records.FirstOrDefault(item => SameKey(item, path, pageId));
                if (record == null)
                {
                    if (records.Count >= MaximumRecords) throw new InvalidDataException("窗口位置记录数量超过限制。");
                    record = new Record { SolutionPath = path, PageId = pageId };
                    records.Add(record);
                }
                record.Placement = snapshot;
                var document = new JObject { ["Version"] = 1, ["Records"] = new JArray(records.Select(ToJson)) };
                byte[] bytes = Utf8.GetBytes(document.ToString(Formatting.None));
                if (bytes.Length > MaximumBytes) throw new InvalidDataException("窗口位置配置超过2MB限制。");
                string directory = Path.GetDirectoryName(FilePath);
                Directory.CreateDirectory(directory);
                string temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush(true);
                    }
                    if (File.Exists(FilePath)) File.Replace(temporary, FilePath, null, true);
                    else File.Move(temporary, FilePath);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }

        /// <summary>读取受限字节和固定结构，不使用对象类型反序列化或全局JSON设置。</summary>
        private List<Record> ReadRecords()
        {
            byte[] bytes;
            try
            {
                using (var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Length <= 0 || stream.Length > MaximumBytes)
                        throw new InvalidDataException("窗口位置配置为空或超过2MB限制。");
                    bytes = new byte[(int)stream.Length];
                    int offset = 0;
                    while (offset < bytes.Length)
                    {
                        int read = stream.Read(bytes, offset, bytes.Length - offset);
                        if (read == 0) throw new InvalidDataException("窗口位置配置不完整。");
                        offset += read;
                    }
                    if (stream.ReadByte() != -1) throw new InvalidDataException("窗口位置配置读取期间发生变化。");
                }
            }
            catch (FileNotFoundException) { return new List<Record>(); }
            catch (DirectoryNotFoundException) { return new List<Record>(); }
            try
            {
                using (var input = new StringReader(Utf8.GetString(bytes).TrimStart('\uFEFF')))
                using (var reader = new JsonTextReader(input) { MaxDepth = 12, DateParseHandling = DateParseHandling.None })
                {
                    JObject document = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new InvalidDataException("窗口位置配置包含额外内容。");
                    RequireFields(document, "Version", "Records");
                    if (ReadInt(document["Version"]) != 1) throw new InvalidDataException("不支持此窗口位置配置版本。");
                    var array = document["Records"] as JArray;
                    if (array == null || array.Count > MaximumRecords) throw new InvalidDataException("窗口位置记录集合无效。");
                    var records = new List<Record>();
                    var keys = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                    foreach (JToken token in array)
                    {
                        JObject item = RequireFields(token, "SolutionPath", "PageId", "Placement");
                        string path = NormalizeSolutionPath(ReadText(item["SolutionPath"], 32767, "方案路径"));
                        string page = ReadText(item["PageId"], 128, "页面编号");
                        HashSet<string> pages;
                        if (!keys.TryGetValue(path, out pages)) keys.Add(path, pages = new HashSet<string>(StringComparer.Ordinal));
                        if (!pages.Add(page)) throw new InvalidDataException("窗口位置配置含有重复页面记录。");
                        JObject value = RequireFields(item["Placement"], "DeviceName", "WorkingArea", "NormalBounds", "Maximized");
                        if (value["Maximized"].Type != JTokenType.Boolean) throw new InvalidDataException("窗口最大化状态格式无效。");
                        var placement = new RuntimeWindowPlacement { DeviceName = ReadText(value["DeviceName"], 256, "显示器名称"),
                            WorkingArea = ReadRectangle(value["WorkingArea"]), NormalBounds = ReadRectangle(value["NormalBounds"]),
                            Maximized = (bool)value["Maximized"] };
                        ValidatePlacement(placement);
                        records.Add(new Record { SolutionPath = path, PageId = page, Placement = placement });
                    }
                    return records;
                }
            }
            catch (JsonException ex) { throw new InvalidDataException("窗口位置配置JSON格式无效。", ex); }
            catch (DecoderFallbackException ex) { throw new InvalidDataException("窗口位置配置编码无效。", ex); }
        }

        /// <summary>用于单次读改写的内部记录，不在实例间缓存可变对象。</summary>
        private sealed class Record
        {
            /// <summary>规范化方案路径。</summary>
            internal string SolutionPath;
            /// <summary>稳定页面编号。</summary>
            internal string PageId;
            /// <summary>独立窗口位置。</summary>
            internal RuntimeWindowPlacement Placement;
        }

        /// <summary>方案路径按Windows大小写规则比较，页面编号精确比较。</summary>
        private static bool SameKey(Record record, string path, string page)
        { return string.Equals(record.SolutionPath, path, StringComparison.OrdinalIgnoreCase) && record.PageId == page; }

        /// <summary>将相对路径、点段及分隔符统一为完整路径，拒绝不合法路径。</summary>
        private static string NormalizeSolutionPath(string path)
        {
            ValidateText(path, 32767, "方案路径");
            try { return Path.GetFullPath(path); }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            { throw new InvalidDataException("方案路径格式无效。", ex); }
        }

        /// <summary>验证持久化位置，不接受空名称、非法尺寸及坐标溢出。</summary>
        private static void ValidatePlacement(RuntimeWindowPlacement placement)
        {
            if (placement == null) throw new InvalidDataException("窗口位置不能为空。");
            ValidateText(placement.DeviceName, 256, "显示器名称");
            if (!RuntimeWindowPlacementGeometry.IsValidRectangle(placement.WorkingArea) ||
                !RuntimeWindowPlacementGeometry.IsValidRectangle(placement.NormalBounds))
                throw new InvalidDataException("窗口位置尺寸或坐标无效。");
        }

        /// <summary>限制字符串长度并拒绝空标识和控制字符。</summary>
        private static void ValidateText(string value, int maximum, string name)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || value.Any(char.IsControl))
                throw new InvalidDataException(name + "为空、过长或含有非法字符。");
        }

        /// <summary>只接受约定属性，类型元数据和未知字段均拒绝。</summary>
        private static JObject RequireFields(JToken token, params string[] fields)
        {
            var value = token as JObject;
            if (value == null || value.Properties().Count() != fields.Length || fields.Any(field => value[field] == null))
                throw new InvalidDataException("窗口位置配置属性无效。");
            return value;
        }

        /// <summary>只接受有界整数，拒绝浮点数、字符串及整数溢出。</summary>
        private static int ReadInt(JToken value)
        {
            if (value == null || value.Type != JTokenType.Integer ||
                !int.TryParse(value.ToString(Formatting.None), System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out int number))
                throw new InvalidDataException("窗口位置配置整数无效。");
            return number;
        }

        /// <summary>读取并验证明确的字符串类型。</summary>
        private static string ReadText(JToken value, int maximum, string name)
        {
            if (value == null || value.Type != JTokenType.String) throw new InvalidDataException(name + "类型无效。");
            string text = (string)value;
            ValidateText(text, maximum, name);
            return text;
        }

        /// <summary>从固定的四个整数属性读取矩形。</summary>
        private static Rectangle ReadRectangle(JToken token)
        {
            JObject rectangle = RequireFields(token, "X", "Y", "Width", "Height");
            return new Rectangle(ReadInt(rectangle["X"]), ReadInt(rectangle["Y"]), ReadInt(rectangle["Width"]), ReadInt(rectangle["Height"]));
        }

        /// <summary>只序列化约定记录字段，不调用任意对象转换器。</summary>
        private static JObject ToJson(Record record)
        {
            RuntimeWindowPlacement value = record.Placement;
            return new JObject { ["SolutionPath"] = record.SolutionPath, ["PageId"] = record.PageId,
                ["Placement"] = new JObject { ["DeviceName"] = value.DeviceName, ["WorkingArea"] = ToJson(value.WorkingArea),
                    ["NormalBounds"] = ToJson(value.NormalBounds), ["Maximized"] = value.Maximized } };
        }

        /// <summary>矩形仅写入必要整数，避免平台转换器隐式格式。</summary>
        private static JObject ToJson(Rectangle value)
        { return new JObject { ["X"] = value.X, ["Y"] = value.Y, ["Width"] = value.Width, ["Height"] = value.Height }; }
    }
}
