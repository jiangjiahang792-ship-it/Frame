using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace TDJS_Vision
{
    public sealed class LanguageInfo
    {
        public string Culture { get; set; }
        public string DisplayName { get; set; }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    public static class LanguageManager
    {
        private const string DefaultCulture = "zh-CN";
        private const string ConfigFileName = "language.config";
        private static Dictionary<string, string> Texts = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> DefaultTexts = new Dictionary<string, string>();
        /// <summary>启动时读取全部语言包，后续切换只使用内存快照。</summary>
        private static readonly Dictionary<string, Dictionary<string, string>> LoadedLanguages = new Dictionary<string, Dictionary<string, string>>();
        /// <summary>启动时解析的可用语言列表。</summary>
        private static readonly List<LanguageInfo> AvailableLanguages = new List<LanguageInfo>();
        /// <summary>保证多次初始化入口不会重复读取文件。</summary>
        private static bool initialized;
        /// <summary>初始化同步锁。</summary>
        private static readonly object InitializationLock = new object();

        public static event EventHandler LanguageChanged;

        public static string CurrentCulture { get; private set; } = DefaultCulture;

        public static string LanguageDirectory
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Languages"); }
        }

        private static string ConfigFilePath
        {
            get { return Path.Combine(Application.UserAppDataPath, ConfigFileName); }
        }

        public static void Initialize()
        {
            lock (InitializationLock)
            {
                if (initialized) return;
                if (Directory.Exists(LanguageDirectory))
                    foreach (string file in Directory.GetFiles(LanguageDirectory, "*.json"))
                    {
                        try
                        {
                            LanguageInfo info = ReadLanguageInfo(file);
                            if (string.IsNullOrEmpty(info.Culture)) continue;
                            var values = new Dictionary<string, string>();
                            LoadLanguage(info.Culture, values);
                            LoadedLanguages[info.Culture] = values;
                            AvailableLanguages.Add(info);
                        }
                        catch (JsonException) { /* 无效语言包不阻止软件启动。 */ }
                        catch (IOException) { /* 无法读取的语言包回退中文。 */ }
                    }
                if (LoadedLanguages.TryGetValue(DefaultCulture, out var defaults))
                    foreach (var pair in defaults) DefaultTexts[pair.Key] = pair.Value;
                initialized = true;
                SetLanguage(LoadSavedCulture(), false);
            }
        }

        public static List<LanguageInfo> GetAvailableLanguages()
        {
            Initialize();
            return AvailableLanguages
                .OrderBy(info => info.Culture == DefaultCulture ? 0 : 1)
                .ThenBy(info => info.DisplayName)
                .ToList();
        }

        public static void SetLanguage(string culture, bool save = true)
        {
            Initialize();
            if (string.IsNullOrWhiteSpace(culture))
                culture = DefaultCulture;

            // 先完整构造再发布，后台检测线程不会读到Clear后的中间状态。
            if (!LoadedLanguages.TryGetValue(culture, out var nextTexts))
            {
                culture = DefaultCulture;
                nextTexts = DefaultTexts;
            }
            Texts = nextTexts;
            CurrentCulture = culture;
            displayTranslator = new DisplayTranslator(nextTexts, culture);

            if (save)
                SaveCulture(culture);

            LanguageChanged?.Invoke(null, EventArgs.Empty);
        }

        public static string T(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return string.Empty;

            if (Texts.TryGetValue(key, out var text))
                return text;

            if (DefaultTexts.TryGetValue(key, out var defaultText))
                return defaultText;

            return key;
        }

        public static string Format(string key, params object[] args)
        {
            return string.Format(T(key), args);
        }

        /// <summary>取得默认中文资源，仅用于辨认内置默认名称，不改变方案中的名称。</summary>
        internal static string GetDefaultText(string key)
        {
            return DefaultTexts.TryGetValue(key, out var value) ? value : key;
        }

        /// <summary>当前语言的只读显示词典，切换时整体替换。</summary>
        private static IDisplayTextTranslator displayTranslator = new DisplayTranslator(new Dictionary<string, string>(), DefaultCulture);

        /// <summary>翻译显示文本；不用于设备协议、配置保存、用户输入或业务比较。</summary>
        /// <param name="text">待显示的中文文本或已经翻译的文本。</param>
        /// <returns>当前语言的显示文字。</returns>
        public static string TranslateDisplay(string text)
        {
            return displayTranslator.Translate(text);
        }

        /// <summary>翻译ROI前缀，保留冒号和空白；未知自定义前缀原样显示。</summary>
        public static string TranslatePrefix(string prefix)
        {
            if (string.IsNullOrEmpty(prefix)) return prefix;
            string translated = T(prefix);
            if (translated != prefix) return translated;
            string key = prefix.TrimEnd(' ', ':', '：', '\t');
            return T(key) + prefix.Substring(key.Length);
        }

        /// <summary>翻译日志正文，保留时间、等级和落盘原文。</summary>
        public static string TranslateLogLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return line;
            Match header = Regex.Match(line, @"\A(?<header>\d{4}[-/]\d{2}[-/]\d{2}[^\r\n]*?\s+(?:Info|Debug|Warn|Exception|Fatal)\s+)(?<body>[\s\S]*)\z");
            return header.Success ? header.Groups["header"].Value + TranslateDisplay(header.Groups["body"].Value) : TranslateDisplay(line);
        }

        public static void Bind(Control control, string textKey)
        {
            if (control != null)
                control.Tag = textKey;
        }

        public static void Bind(ToolStripItem item, string textKey, string toolTipKey = null)
        {
            if (item == null)
                return;

            item.Tag = new LanguageBinding(textKey, toolTipKey);
        }

        public static void Apply(Control control)
        {
            if (control == null)
                return;

            ApplyControl(control);

            if (control is ToolStrip toolStrip)
                ApplyToolStrip(toolStrip);

            foreach (Control child in control.Controls)
                Apply(child);
        }

        public static void ApplyToolStrip(ToolStrip toolStrip)
        {
            if (toolStrip == null)
                return;

            foreach (ToolStripItem item in toolStrip.Items)
                ApplyToolStripItem(item);
        }

        public static void ApplyToolStripItem(ToolStripItem item)
        {
            if (item == null)
                return;

            if (item.Tag is LanguageBinding binding)
            {
                if (!string.IsNullOrWhiteSpace(binding.TextKey))
                    item.Text = T(binding.TextKey);
                if (!string.IsNullOrWhiteSpace(binding.ToolTipKey))
                    item.ToolTipText = T(binding.ToolTipKey);
            }
            else if (item.Tag is string key)
            {
                item.Text = T(key);
            }

            if (item is ToolStripDropDownItem dropDownItem)
            {
                foreach (ToolStripItem child in dropDownItem.DropDownItems)
                    ApplyToolStripItem(child);
            }
        }

        private static void ApplyControl(Control control)
        {
            if (control.Tag is string key)
                control.Text = T(key);
        }

        private static LanguageInfo ReadLanguageInfo(string file)
        {
            try
            {
                var json = JObject.Parse(File.ReadAllText(file));
                var meta = json["$meta"];
                var culture = meta?["culture"]?.ToString() ?? Path.GetFileNameWithoutExtension(file);
                var displayName = meta?["displayName"]?.ToString() ?? culture;
                return new LanguageInfo { Culture = culture, DisplayName = displayName };
            }
            catch
            {
                return new LanguageInfo();
            }
        }

        private static void LoadLanguage(string culture, Dictionary<string, string> target)
        {
            target.Clear();

            var file = GetLanguageFile(culture);
            if (!File.Exists(file))
                return;

            var json = JsonConvert.DeserializeObject<Dictionary<string, JToken>>(File.ReadAllText(file));
            if (json == null)
                return;

            foreach (var pair in json)
            {
                if (pair.Key == "$meta")
                    continue;

                target[pair.Key] = pair.Value.Type == JTokenType.String
                    ? pair.Value.ToString()
                    : pair.Value.ToString(Formatting.None);
            }
        }

        private static string GetLanguageFile(string culture)
        {
            return Path.Combine(LanguageDirectory, culture + ".json");
        }

        private static string LoadSavedCulture()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                    return File.ReadAllText(ConfigFilePath).Trim();
            }
            catch
            {
            }

            return DefaultCulture;
        }

        private static void SaveCulture(string culture)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigFilePath));
                File.WriteAllText(ConfigFilePath, culture);
            }
            catch
            {
            }
        }

        private sealed class LanguageBinding
        {
            public LanguageBinding(string textKey, string toolTipKey)
            {
                TextKey = textKey;
                ToolTipKey = toolTipKey;
            }

            public string TextKey { get; }
            public string ToolTipKey { get; }
        }
    }
}
