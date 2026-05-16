using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private static readonly Dictionary<string, string> Texts = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> DefaultTexts = new Dictionary<string, string>();

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
            LoadLanguage(DefaultCulture, DefaultTexts);

            var savedCulture = LoadSavedCulture();
            if (!File.Exists(GetLanguageFile(savedCulture)))
                savedCulture = DefaultCulture;

            SetLanguage(savedCulture, false);
        }

        public static List<LanguageInfo> GetAvailableLanguages()
        {
            if (!Directory.Exists(LanguageDirectory))
                return new List<LanguageInfo>();

            return Directory.GetFiles(LanguageDirectory, "*.json")
                .Select(ReadLanguageInfo)
                .Where(info => !string.IsNullOrWhiteSpace(info.Culture))
                .OrderBy(info => info.Culture == DefaultCulture ? 0 : 1)
                .ThenBy(info => info.DisplayName)
                .ToList();
        }

        public static void SetLanguage(string culture, bool save = true)
        {
            if (string.IsNullOrWhiteSpace(culture))
                culture = DefaultCulture;

            LoadLanguage(culture, Texts);
            CurrentCulture = culture;

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
