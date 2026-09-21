using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace TDJS_Vision.Node._7_ResultProcessing.NamedImageSave
{
    /// <summary>一次运行冻结后的保存路径和名称。</summary>
    public sealed class NamedImagePath
    {
        /// <summary>完整文件夹路径。</summary>
        public string DirectoryPath { get; set; }
        /// <summary>不含自动.jpg扩展名的基础名称。</summary>
        public string ImageName { get; set; }
    }

    /// <summary>纯路径计算，不创建目录或访问图像资源，支持替换订阅读取器。</summary>
    public static class NamedImagePathBuilder
    {
        /// <summary>Windows文件名非法字符缓存。</summary>
        private static readonly char[] InvalidCharacters = Path.GetInvalidFileNameChars();

        /// <summary>验证可保存的静态配置，不要求订阅源已运行。</summary>
        public static void Validate(NodeParamNamedImageSave param)
        {
            if (param == null) throw new InvalidOperationException("请先保存命名存图参数。");
            string root = param.DriveRoot ?? string.Empty;
            if (root.Length != 3 || !char.IsLetter(root[0]) || root[1] != ':' || root[2] != '\\' || root[0] > 127)
                throw new InvalidOperationException("请选择有效盘符，例如D:\\。");
            if (param.Folders == null || param.Folders.Count == 0)
                throw new InvalidOperationException("请至少添加一行文件夹。");
            if (param.JpegQuality < 1 || param.JpegQuality > 100)
                throw new InvalidOperationException("JPEG质量必须为1到100。");
            if (param.ImageSource == null || param.ImageSource.SourceNodeId <= 0 || string.IsNullOrWhiteSpace(param.ImageSource.PropertyPath))
                throw new InvalidOperationException("请选择需要保存的图像订阅。");
            foreach (var row in param.Folders)
            {
                if (row?.EffectiveMode == NamedImageValueMode.BoolOkNg &&
                    (row.Source == null || row.Source.SourceNodeId <= 0 || string.IsNullOrWhiteSpace(row.Source.PropertyPath)))
                    throw new InvalidOperationException("OK/NG目录行必须选择布尔结果订阅。");
            }
            foreach (var part in GetFileNameParts(param))
            {
                if (part?.EffectiveMode == NamedImageValueMode.BoolOkNg &&
                    (part.Source == null || part.Source.SourceNodeId <= 0 || string.IsNullOrWhiteSpace(part.Source.PropertyPath)))
                    throw new InvalidOperationException("OK/NG文件名段必须选择布尔结果订阅。");
            }
        }

        /// <summary>按行序组成多级文件夹，每个订阅只读取一次，异常名称记录错误并回退。</summary>
        public static NamedImagePath Build(NodeParamNamedImageSave param, Func<NamedImageSource, object> read,
            DateTime time, Action<string> reportError)
        {
            Validate(param);
            string directory = param.DriveRoot;
            string timestamp = time.ToString("yyyy-MM-dd_HH-mm-ss-fff", CultureInfo.InvariantCulture);
            string currentDate = time.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            for (int i = 0; i < param.Folders.Count; i++)
            {
                string value = GetText(param.Folders[i], read, currentDate);
                directory = Path.Combine(directory, Normalize(value, "第" + (i + 1) + "行文件夹", timestamp, reportError));
            }
            string name = BuildFileName(param, read, currentDate, timestamp, reportError);
            return new NamedImagePath { DirectoryPath = directory, ImageName = name };
        }

        /// <summary>按文件名片段顺序拼接基础文件名，旧方案没有片段时继续使用单个FileName。</summary>
        private static string BuildFileName(NodeParamNamedImageSave param, Func<NamedImageSource, object> read,
            string currentDate, string timestamp, Action<string> reportError)
        {
            var parts = GetFileNameParts(param).ToList();
            if (parts.Count == 1 && ReferenceEquals(parts[0], param.FileName))
            {
                string legacyName = GetText(parts[0], read, currentDate);
                if (parts[0]?.EffectiveMode == NamedImageValueMode.Manual && string.IsNullOrWhiteSpace(legacyName)) legacyName = timestamp;
                return Normalize(legacyName, "图像名称", timestamp, reportError);
            }
            var builder = new System.Text.StringBuilder();
            for (int i = 0; i < parts.Count; i++)
            {
                string value = GetText(parts[i], read, currentDate);
                if (parts[i]?.EffectiveMode == NamedImageValueMode.Manual && string.IsNullOrWhiteSpace(value)) continue;
                builder.Append(Normalize(value, "图像名称第" + (i + 1) + "段", timestamp, reportError));
            }
            string name = builder.ToString();
            if (string.IsNullOrWhiteSpace(name)) name = timestamp;
            return Normalize(name, "图像名称", timestamp, reportError);
        }

        /// <summary>返回实际使用的文件名片段，保证旧方案FileName字段可直接生效。</summary>
        private static IEnumerable<NamedImageValue> GetFileNameParts(NodeParamNamedImageSave param)
        {
            if (param.FileNameParts != null && param.FileNameParts.Count > 0)
            {
                if (param.FileNameParts.Count == 1 && IsEmptyManual(param.FileNameParts[0]) && !IsEmptyManual(param.FileName))
                    return new[] { param.FileName };
                return param.FileNameParts;
            }
            return new[] { param.FileName ?? new NamedImageValue() };
        }

        /// <summary>识别旧方案反序列化时由默认值生成的空白文件名片段。</summary>
        private static bool IsEmptyManual(NamedImageValue value)
        {
            return value == null || value.EffectiveMode == NamedImageValueMode.Manual && string.IsNullOrWhiteSpace(value.Text);
        }

        /// <summary>保留前导零和内部空格，数字使用不变区域格式，普通布尔使用True/False。</summary>
        private static string GetText(NamedImageValue value, Func<NamedImageSource, object> read, string currentDate)
        {
            if (value == null) return string.Empty;
            switch (value.EffectiveMode)
            {
                case NamedImageValueMode.CurrentDate:
                    return currentDate;
                case NamedImageValueMode.Manual:
                    return value.Text;
            }
            if (value.Source == null) throw new InvalidOperationException("订阅来源为空。");
            object result = read(value.Source);
            if (result == null) throw new InvalidOperationException("订阅结果为空：" + value.Source.DisplayName);
            if (value.EffectiveMode == NamedImageValueMode.BoolOkNg)
            {
                if (!(result is bool ok)) throw new InvalidOperationException("OK/NG目录行只支持布尔结果：" + value.Source.DisplayName);
                return ok ? "OK" : "NG";
            }
            if (!(result is string) && !(result is bool) && !(result is char) && !IsNumber(result.GetType()))
                throw new InvalidOperationException("文件夹或文件名只支持文本、数值、布尔结果：" + value.Source.DisplayName);
            return Convert.ToString(result, CultureInfo.InvariantCulture);
        }

        /// <summary>只接受基础数值，避免把任意对象的ToString当作文件名。</summary>
        public static bool IsNumber(Type type)
        {
            TypeCode code = Type.GetTypeCode(type);
            return code >= TypeCode.SByte && code <= TypeCode.Decimal;
        }

        /// <summary>每行只表示一级目录，拦截路径分隔符、保留名和穿越片段，保证留在所选根目录。</summary>
        private static string Normalize(string text, string position, string timestamp, Action<string> reportError)
        {
            string value = text ?? string.Empty;
            bool invalid = string.IsNullOrWhiteSpace(value) || value.Length > 240 || value == "." || value == ".." ||
                value.IndexOfAny(InvalidCharacters) >= 0 || value.EndsWith(".", StringComparison.Ordinal) || value.EndsWith(" ", StringComparison.Ordinal);
            string stem = value.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
            invalid |= stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL" || stem == "CONIN$" || stem == "CONOUT$" ||
                (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) && "123456789¹²³".IndexOf(stem[3]) >= 0);
            if (!invalid) return value;
            string fallback = "非法字符_" + timestamp;
            reportError?.Invoke(position + "无效；原内容=" + JsonConvert.SerializeObject(text) + "；改用=" + fallback + "，继续保存图片。");
            return fallback;
        }
    }
}
