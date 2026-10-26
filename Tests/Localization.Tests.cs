using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using TDJS_Vision;

/// <summary>离线验证显示翻译、语言文件启动快照和业务值隔离。</summary>
internal static class LocalizationTests
{
    /// <summary>已完成的断言数量。</summary>
    private static int assertions;

    /// <summary>断言失败时终止测试。</summary>
    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }

    /// <summary>运行离线验证，不打开设备、模型或生产方案。</summary>
    [STAThread]
    private static int Main()
    {
        try
        {
            ValidateResources();
            LanguageManager.Initialize();
            Check(LanguageManager.GetAvailableLanguages().Any(item => item.Culture == "ja-JP"), "未发现日语语言包");
            LanguageManager.SetLanguage("en-US", false);
            Check(LanguageManager.T("套筒压接位置") == "Sleeve crimp position", "套筒压接位置英文翻译缺失");
            Check(LanguageManager.T("护套剥离长度") == "Sheath stripping length", "护套剥离长度英文翻译缺失");
            Check(LanguageManager.TranslatePrefix("胶皮长度： ") == "Rubber length： ", "ROI前缀翻译及标点保留");
            Check(LanguageManager.TranslatePrefix("前后仰角度：") == "Front/rear tilt angle: ", "方案自定义角度前缀英文翻译缺失");
            Check(LanguageManager.TranslateDisplay("4.无飞丝") == "4. No flying wire", "方案自定义布尔文本英文翻译缺失");
            Check(LanguageManager.TranslatePrefix("客户自定义前缀：") == "客户自定义前缀：", "未知前缀不得被拆词翻译");
            string raw = "节点(3.用户节点)运行失败！原因：第三方中文错误";
            Check(LanguageManager.TranslateDisplay(raw) == "Node (3.用户节点) failed: 第三方中文错误", "日志句式应保留用户名称和异常参数");
            string line = "2026-10-10 15:01:02.123456    Exception    " + raw;
            Check(LanguageManager.TranslateLogLine(line) == "2026-10-10 15:01:02.123456    Exception    Node (3.用户节点) failed: 第三方中文错误", "日志时间和等级应保持不变");
            Check(LanguageManager.Format("{0}: {1} 设定值: [{2}, {3}]", "Item", "1.20mm", 1, 2) == "Item: 1.20mm Limits: [1, 2]", "TDAI设定范围翻译");
            Check(LanguageManager.Format("红黑线序: {0}", "OK").StartsWith(LanguageManager.T("红黑线序") + ":"), "TDAI线序文本去重前缀不一致");
            ValidateControls();
            ValidateStartupSnapshot();
            LanguageManager.SetLanguage("ja-JP", false);
            Check(LanguageManager.TranslateDisplay("节点(1.用户节点)运行取消！") == "ノード（1.用户节点）をキャンセルしました。", "日语日志翻译");
            LanguageManager.SetLanguage("zh-CN", false);
            Check(LanguageManager.TranslateDisplay(raw) == raw, "中文回切必须恢复原文");
            LanguageManager.SetLanguage("en-US", false);
            var timer = Stopwatch.StartNew();
            for (int i = 0; i < 5000; i++) LanguageManager.TranslateDisplay("节点(1.测试)运行成功！(" + i + " ms)");
            Console.WriteLine("动态日志5000条翻译耗时：" + timer.ElapsedMilliseconds + "ms");
            Console.WriteLine("本地化验证通过，断言：" + assertions);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    /// <summary>检查三语键集合、译文及格式参数，避免远程替换后出现格式错误。</summary>
    private static void ValidateResources()
    {
        var packs = new[] { "zh-CN", "en-US", "ja-JP" }.Select(culture =>
            JObject.Parse(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Languages", culture + ".json")))).ToArray();
        string[] keys = packs[0].Properties().Select(property => property.Name).OrderBy(key => key).ToArray();
        foreach (JObject pack in packs.Skip(1))
        {
            Check(keys.SequenceEqual(pack.Properties().Select(property => property.Name).OrderBy(key => key)), "语言资源键不一致");
            foreach (string key in keys.Where(key => key != "$meta"))
            {
                string translated = (string)pack[key];
                Check(!string.IsNullOrWhiteSpace(translated), "空译文：" + key);
                string[] expected = Regex.Matches((string)packs[0][key], @"\{\d+(?:[^{}]*)\}").Cast<Match>().Select(match => match.Value).OrderBy(value => value).ToArray();
                string[] actual = Regex.Matches(translated, @"\{\d+(?:[^{}]*)\}").Cast<Match>().Select(match => match.Value).OrderBy(value => value).ToArray();
                Check(expected.SequenceEqual(actual), "格式参数不一致：" + key);
            }
        }
    }

    /// <summary>验证已打开控件切换、动态文字、新增控件以及输入值和事件隔离。</summary>
    private static void ValidateControls()
    {
        LanguageManager.SetLanguage("zh-CN", false);
        using (var form = new Form())
        using (var label = new Label { Text = "复制节点" })
        using (var input = new TextBox { Text = "用户输入中文" })
        using (var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList })
        using (var grid = new DataGridView())
        using (var menu = new ContextMenuStrip())
        {
            combo.Items.AddRange(new object[] { "关闭", "打开" });
            combo.SelectedIndex = 0;
            int selectionEvents = 0;
            combo.SelectedIndexChanged += (sender, args) => selectionEvents++;
            menu.Items.Add("复制节点");
            label.ContextMenuStrip = menu;
            grid.Columns.Add("value", "当前值");
            form.Controls.AddRange(new Control[] { label, input, combo, grid });
            var userName = new Label { Text = "1.客户失败：自定义名称" };
            UiLocalization.PreserveText(userName);
            form.Controls.Add(userName);
            UiLocalization.Attach(form);
            LanguageManager.SetLanguage("en-US", false);
            Check(label.Text == "Copy node", "静态标签未翻译");
            Check(menu.Items[0].Text == "Copy node", "菜单未翻译");
            Check(grid.Columns[0].HeaderText == "Current Value", "表格标题未翻译");
            Check(input.Text == "用户输入中文", "用户输入被改写");
            Check(userName.Text == "1.客户失败：自定义名称", "用户标题被识别为提示句式");
            Check(combo.Text == "关闭" && (string)combo.SelectedItem == "关闭" && selectionEvents == 0, "语言切换改变了选项业务值或触发设备事件");
            label.Text = "打开手动调参失败：设备离线";
            Check(label.Text == "Failed to open manual tuning: 设备离线", "动态文字未翻译");
            var added = new Label { Text = "粘贴节点" };
            form.Controls.Add(added);
            Check(added.Text == "Paste node", "运行时新增控件未翻译");
            LanguageManager.SetLanguage("ja-JP", false);
            Check(added.Text == "ノードを貼り付け", "已打开控件未切换日语");
            Check(menu.Items[0].Text == "ノードをコピー", "菜单未切换日语");
            LanguageManager.SetLanguage("zh-CN", false);
            Check(label.Text == "打开手动调参失败：设备离线" && added.Text == "粘贴节点", "中文源文字丢失");
            Check(selectionEvents == 0, "语言切换触发了选项变更事件");
        }
    }

    /// <summary>临时替换隔离测试目录的语言包，验证同一进程不重新读取。</summary>
    private static void ValidateStartupSnapshot()
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Languages", "ja-JP.json");
        string original = File.ReadAllText(path);
        try
        {
            var replacement = JObject.Parse(original);
            replacement["Common.Language"] = "更新后的测试译文";
            File.WriteAllText(path, replacement.ToString());
            LanguageManager.Initialize();
            LanguageManager.SetLanguage("ja-JP", false);
            Check(LanguageManager.T("Common.Language") == "言語", "运行中重新读取了已替换的语言文件");
        }
        finally { File.WriteAllText(path, original); }
    }
}
