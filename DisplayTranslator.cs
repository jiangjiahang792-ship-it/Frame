using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace TDJS_Vision
{
    /// <summary>显示层翻译接口，业务数据和设备协议不经过此接口。</summary>
    internal interface IDisplayTextTranslator
    {
        /// <summary>翻译完整文本或语言库中已定义的完整句式。</summary>
        string Translate(string text);
    }

    /// <summary>启动时构建的只读翻译快照，不访问网络或热加载文件。</summary>
    internal sealed class DisplayTranslator : IDisplayTextTranslator
    {
        /// <summary>格式参数识别器，支持带格式和对齐的参数。</summary>
        private static readonly Regex Placeholder = new Regex(@"(?<!\{)\{(?<index>\d+)(?:,[^{}:]+)?(?::[^{}]+)?\}(?!\})", RegexOptions.Compiled);
        /// <summary>完整文本的精确翻译表。</summary>
        private readonly Dictionary<string, string> entries;
        /// <summary>包含参数的完整句式。</summary>
        private readonly Template[] templates;
        /// <summary>缓存已经显示的动态消息，达到容量后不再添加。</summary>
        private readonly ConcurrentDictionary<string, string> cache = new ConcurrentDictionary<string, string>();
        /// <summary>中文模式直接返回原文。</summary>
        private readonly bool chinese;

        /// <summary>将语言文件内容编译成独立的显示快照。</summary>
        public DisplayTranslator(Dictionary<string, string> values, string culture)
        {
            entries = values;
            chinese = culture == "zh-CN";
            templates = chinese ? new Template[0] : values
                .Where(pair => Placeholder.IsMatch(pair.Key) && Regex.IsMatch(pair.Key, @"[\u4e00-\u9fff]"))
                .OrderByDescending(pair => Placeholder.Replace(pair.Key, string.Empty).Length)
                .Select(pair => new Template(pair.Key, pair.Value)).ToArray();
        }

        /// <summary>只匹配完整词条和完整句式，绝不拆词翻译用户名称或异常参数。</summary>
        public string Translate(string text)
        {
            if (chinese || string.IsNullOrEmpty(text)) return text;
            if (entries.TryGetValue(text, out var exact)) return exact;
            if (!Regex.IsMatch(text, @"[\u4e00-\u9fff]")) return text;
            if (cache.TryGetValue(text, out var previous)) return previous;
            foreach (Template template in templates)
            {
                string translated = template.TryTranslate(text);
                if (translated == null) continue;
                if (cache.Count < 2048) cache.TryAdd(text, translated);
                return translated;
            }
            return text;
        }

        /// <summary>保留参数内容的完整句式匹配器。</summary>
        private sealed class Template
        {
            /// <summary>句式中最长的固定片段，用于快速排除不匹配消息。</summary>
            private readonly string anchor;
            /// <summary>锚定完整消息的匹配表达式。</summary>
            private readonly Regex pattern;
            /// <summary>目标语言句式。</summary>
            private readonly string translation;

            /// <summary>构造有执行时间上限的句式匹配器。</summary>
            public Template(string source, string target)
            {
                translation = target;
                var builder = new StringBuilder(@"\A");
                var literals = new List<string>();
                int offset = 0;
                foreach (Match match in Placeholder.Matches(source))
                {
                    string literal = source.Substring(offset, match.Index - offset).Replace("{{", "{").Replace("}}", "}");
                    literals.Add(literal);
                    builder.Append(Regex.Escape(literal));
                    builder.Append("(?<p").Append(match.Groups["index"].Value).Append(@">[\s\S]*?)");
                    offset = match.Index + match.Length;
                }
                string tail = source.Substring(offset).Replace("{{", "{").Replace("}}", "}");
                literals.Add(tail);
                builder.Append(Regex.Escape(tail)).Append(@"\z");
                anchor = literals.OrderByDescending(value => value.Length).FirstOrDefault() ?? string.Empty;
                pattern = new Regex(builder.ToString(), RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(20));
            }

            /// <summary>匹配成功时仅重排参数，参数原文不再递归翻译。</summary>
            public string TryTranslate(string text)
            {
                if (text.IndexOf(anchor, StringComparison.Ordinal) < 0) return null;
                try
                {
                    Match match = pattern.Match(text);
                    if (!match.Success) return null;
                    return Placeholder.Replace(translation, parameter => match.Groups["p" + parameter.Groups["index"].Value].Value)
                        .Replace("{{", "{").Replace("}}", "}");
                }
                catch (RegexMatchTimeoutException) { return null; }
            }
        }
    }
}
