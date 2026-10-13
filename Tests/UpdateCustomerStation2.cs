using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>定向迁移客户工位2配置，仅替换指定JSON数组，保留其它原始文本及检测参数。</summary>
internal static class UpdateCustomerStation2
{
    /// <summary>更正上一版第8、9项名称时，校验原始顺序以防重复迁移。</summary>
    private static bool CorrectL6;
    /// <summary>指定目标工位，默认保留原工位2迁移行为。</summary>
    private static string Station = "工位2";
    /// <summary>迁移后每份现场配置的预期项目数。</summary>
    private static int ExpectedItems = 9;
    /// <summary>仅升级指定方案的补偿字段及工位1名称，不更新独立items。</summary>
    private static bool CompensationSolutionOnly;
    /// <summary>仅名称变更，原项的上下限、启用状态和当前值保持不变。</summary>
    private static readonly Dictionary<string, string> Renames = new Dictionary<string, string>
    {
        { "胶皮顶部到屏蔽网顶部的距离", "编组反转代-1" },
        { "胶皮顶部到屏蔽网底部的距离", "编组反转代-2" },
        { "胶皮顶部到绝缘体顶部的距离", "L5_绝缘体长度" },
        { "绝缘体宽度", "L6_箔剥宽度" },
        { "绝缘体长度", "L6_箔剥长度" }
    };

    /// <summary>用户要求删除的六项。</summary>
    private static readonly HashSet<string> Removed = new HashSet<string>
    {
        "L1-FiLay(绝缘体长度)", "铜环底部到屏蔽网顶部距离", "胶皮顶部到绝缘体底部的距离",
        "屏蔽网高度", "屏蔽网宽度", "L1-ALFoiLay(箔长度)"
    };

    /// <summary>单个数组的结构化变更与原始文本区间。</summary>
    private sealed class Edit
    {
        /// <summary>原数组的JSON路径。</summary>
        public string Path;
        /// <summary>原数组快照。</summary>
        public JArray Before;
        /// <summary>变更后的数组。</summary>
        public JArray After;
        /// <summary>原始字符起点。</summary>
        public int Start;
        /// <summary>原始字符结束位置，不包含末尾。</summary>
        public int End;
    }

    /// <summary>全部校验通过后待写入的文件。</summary>
    private sealed class PendingFile
    {
        /// <summary>完整路径。</summary>
        public string Path;
        /// <summary>原始字节，用于备份和并发修改检测。</summary>
        public byte[] Before;
        /// <summary>已验证的新字节。</summary>
        public byte[] After;
    }

    /// <summary>读取JSON时禁止自动解释日期，避免修改无关字符串。</summary>
    private static JToken Parse(string text)
    {
        using (var reader = new JsonTextReader(new StringReader(text)) { DateParseHandling = DateParseHandling.None })
            return JToken.ReadFrom(reader);
    }

    /// <summary>创建检测数组变更，并逐项核对除名称外的全部字段。</summary>
    private static Edit MigrateItems(JArray source)
    {
        if (CorrectL6 && (source.Count != 9 || (string)source[7]["Name"] != "L6_箔剥长度" ||
            (string)source[8]["Name"] != "L5_绝缘体宽度"))
            throw new Exception("原始第8、9项名称不匹配，禁止重复或混合版本迁移：" + source.Path);
        var after = new JArray();
        foreach (JObject item in source)
        {
            string name = (string)item["Name"];
            if (Removed.Contains(name)) continue;
            var copy = (JObject)item.DeepClone();
            if (Renames.TryGetValue(name, out var renamed)) copy["Name"] = renamed;
            var check = (JObject)copy.DeepClone();
            check["Name"] = name;
            if (!JToken.DeepEquals(check, item)) throw new Exception("检测参数变化：" + name);
            after.Add(copy);
        }
        if (after.Count != ExpectedItems || after.Select(i => (string)i["Name"]).Distinct().Count() != ExpectedItems)
            throw new Exception(Station + "迁移后检测项数量不符：" + source.Path);
        return new Edit { Path = source.Path, Before = (JArray)source.DeepClone(), After = after };
    }

    /// <summary>按原配置增补默认补偿，工位1旧名称定向升级，其余字段保持原样。</summary>
    private static Edit MigrateCompensationItems(JArray source, bool station1)
    {
        var after = (JArray)source.DeepClone();
        foreach (JObject item in after)
        {
            if (item.Property("Compensation") == null) item.Add("Compensation", 0);
            if (station1 && (string)item["Name"] == "屏蔽网高度") item["Name"] = "L2_编组切割长度";
        }
        if (after.Select(i => (string)i["Name"]).Distinct().Count() != after.Count)
            throw new Exception("名称升级后检测项重名：" + source.Path);
        return new Edit { Path = source.Path, Before = (JArray)source.DeepClone(), After = after };
    }

    /// <summary>移除引用删除项的发送行，保留其它发送参数和绝对PLC地址。</summary>
    private static Edit MigrateRows(JArray source)
    {
        var after = new JArray();
        foreach (JObject row in source)
        {
            var references = row.Descendants().OfType<JProperty>().Where(p => p.Name == "ItemName").ToList();
            if (references.Any(p => Removed.Contains((string)p.Value)))
            {
                Console.WriteLine("移除发送行：" + (string)row["Address"] + " / " + (string)row["Source"]?["ItemName"]);
                continue;
            }
            var copy = (JObject)row.DeepClone();
            foreach (var prop in copy.Descendants().OfType<JProperty>().Where(p => p.Name == "ItemName").ToList())
            {
                string oldName = (string)prop.Value;
                if (oldName == null || !Renames.TryGetValue(oldName, out var newName)) continue;
                prop.Value = newName;
                var owner = (JObject)prop.Parent;
                if (owner["DisplayName"]?.Type == JTokenType.String)
                    owner["DisplayName"] = ((string)owner["DisplayName"]).Replace(oldName, newName);
            }
            after.Add(copy);
        }
        return new Edit { Path = source.Path, Before = (JArray)source.DeepClone(), After = after };
    }

    /// <summary>利用JSON读取器定位数组边界，只替换授权数组；再比较完整文档的结构。</summary>
    private static PendingFile Prepare(string path, bool solution)
    {
        byte[] bytes = File.ReadAllBytes(path);
        bool bom = bytes.Length >= 3 && bytes[0] == 239 && bytes[1] == 187 && bytes[2] == 191;
        string text = new UTF8Encoding(false, true).GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));
        var original = Parse(text);
        var edits = new List<Edit>();
        if (!solution) edits.Add(MigrateItems((JArray)original));
        else if (CompensationSolutionOnly)
        {
            foreach (var property in ((JObject)original["DetectItemDic"]).Properties())
                edits.Add(MigrateCompensationItems((JArray)property.Value,
                    property.Name.StartsWith("AST-工位1检测项_", StringComparison.Ordinal)));
            foreach (var process in original["ProcessInfos"].Where(p => ((string)p["ProcessName"]).StartsWith("工位1", StringComparison.Ordinal)))
                foreach (var node in ((JContainer)process).Descendants().OfType<JObject>().Where(n => (string)n["NodeType"] == "ResultSend"))
                    foreach (var rows in ((JObject)node["NodeParam"]).Properties().Select(p => p.Value["Rows"]).OfType<JArray>())
                        edits.Add(MigrateRows(rows));
        }
        else
        {
            var dictionary = (JObject)original["DetectItemDic"];
            foreach (var property in dictionary.Properties().Where(p => p.Name.StartsWith("AST-" + Station + "检测项_", StringComparison.Ordinal)))
                edits.Add(MigrateItems((JArray)property.Value));
            if (edits.Count != 8) throw new Exception("方案工位2配置数量不是8。");
            foreach (var process in original["ProcessInfos"].Where(p => ((string)p["ProcessName"]).StartsWith(Station, StringComparison.Ordinal)))
                foreach (var node in ((JContainer)process).Descendants().OfType<JObject>().Where(n => (string)n["NodeType"] == "ResultSend"))
                    foreach (var rows in ((JObject)node["NodeParam"]).Properties().Select(p => p.Value["Rows"]).OfType<JArray>())
                        edits.Add(MigrateRows(rows));
            if (edits.Count != 10) throw new Exception("工位2结果发送节点数量不是2。");
        }
        var expected = original.DeepClone();
        foreach (var edit in edits)
        {
            if (edit.Path == "") expected = edit.After.DeepClone();
            else expected.SelectToken(edit.Path, true).Replace(edit.After.DeepClone());
        }
        var offsets = new List<int> { 0 };
        for (int i = 0; i < text.Length; i++) if (text[i] == '\n') offsets.Add(i + 1);
        using (var reader = new JsonTextReader(new StringReader(text)) { DateParseHandling = DateParseHandling.None })
        {
            while (reader.Read())
            {
                if (reader.TokenType != JsonToken.StartArray) continue;
                var edit = edits.SingleOrDefault(e => e.Path == reader.Path);
                if (edit == null) continue;
                edit.Start = offsets[reader.LineNumber - 1] + reader.LinePosition - 1;
                reader.Skip();
                edit.End = offsets[reader.LineNumber - 1] + reader.LinePosition;
                if (text[edit.Start] != '[' || text[edit.End - 1] != ']' ||
                    !JToken.DeepEquals(Parse(text.Substring(edit.Start, edit.End - edit.Start)), edit.Before))
                    throw new Exception("JSON定位验证失败：" + edit.Path);
            }
        }
        string newline = text.Contains("\r\n") ? "\r\n" : "\n";
        foreach (var edit in edits.OrderByDescending(e => e.Start))
        {
            if (edit.End <= edit.Start) throw new Exception("未定位到数组：" + edit.Path);
            int lineStart = text.LastIndexOf('\n', edit.Start) + 1;
            int indent = 0;
            while (lineStart + indent < text.Length && text[lineStart + indent] == ' ') indent++;
            string replacement = edit.After.ToString(Formatting.Indented).Replace("\r\n", "\n")
                .Replace("\n", newline + new string(' ', indent));
            text = text.Substring(0, edit.Start) + replacement + text.Substring(edit.End);
        }
        if (!JToken.DeepEquals(Parse(text), expected)) throw new Exception("完整文档验证失败：" + path);
        var encoding = new UTF8Encoding(bom);
        Console.WriteLine("验证通过：" + Path.GetFileName(path) + "，数组=" + edits.Count);
        return new PendingFile { Path = path, Before = bytes, After = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray() };
    }

    /// <summary>先验证全部文件，再备份并提交；写入失败时恢复原始字节。</summary>
    private static int Main(string[] args)
    {
        try
        {
            CorrectL6 = args.Contains("--correct-l6");
            if (CorrectL6)
            {
                // 一次性映射，不能在公共归一化中将当前合法的长度键再次改为宽度。
                Renames.Clear();
                Renames.Add("L6_箔剥长度", "L6_箔剥宽度");
                Renames.Add("L5_绝缘体宽度", "L6_箔剥长度");
                Removed.Clear();
            }
            string root = Path.GetFullPath(args[0]);
            CompensationSolutionOnly = args.Contains("--compensation-solution");
            if (CompensationSolutionOnly)
            {
                if (CorrectL6 || args.Contains("--station1-l2")) throw new Exception("方案补偿升级不能与其它模式组合。");
                Station = "方案补偿";
                Renames.Clear();
                Renames.Add("屏蔽网高度", "L2_编组切割长度");
                Removed.Clear();
            }
            if (args.Contains("--station1-l2"))
            {
                if (CorrectL6) throw new Exception("迁移模式不能组合使用。");
                Station = "工位1";
                ExpectedItems = 10;
                Renames.Clear();
                Renames.Add("屏蔽网高度", "L2_编组切割长度");
                Removed.Clear();
            }
            var files = CompensationSolutionOnly ? new string[0] : Directory.GetFiles(Path.Combine(root, "items"), "AST-" + Station + "检测项_*.items");
            if (!CompensationSolutionOnly && files.Length != 8) throw new Exception("工位独立模板数量不是8。");
            var pending = files.Select(f => Prepare(f, false)).ToList();
            pending.Add(Prepare(Path.Combine(root, "Sol", "9_21号方案_组合模块版_默认发OK.Sol"), true));
            if (!args.Contains("--apply")) return 0;
            string backup = Path.Combine(Path.GetDirectoryName(root), Station + "修改备份_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            foreach (var file in pending)
            {
                if (!File.ReadAllBytes(file.Path).SequenceEqual(file.Before)) throw new Exception("文件被其它程序修改：" + file.Path);
                string target = Path.Combine(backup, file.Path.Substring(root.Length + 1));
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.WriteAllBytes(target, file.Before);
            }
            try
            {
                foreach (var file in pending)
                {
                    File.WriteAllBytes(file.Path, file.After);
                    if (!File.ReadAllBytes(file.Path).SequenceEqual(file.After)) throw new Exception("写入复核失败：" + file.Path);
                }
            }
            catch
            {
                foreach (var file in pending) File.WriteAllBytes(file.Path, file.Before);
                throw;
            }
            Console.WriteLine("已更新" + pending.Count + "个文件。备份：" + backup);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
