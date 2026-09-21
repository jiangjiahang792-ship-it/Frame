using System;
using System.Reflection;
using System.Windows.Forms;
using TDJS_Vision;
using TDJS_Vision.Forms.ProcessNew;

/// <summary>验证允许句点的流程重命名、完整名称匹配及空名称拒绝。</summary>
internal static class ProcessRenameTests
{
    /// <summary>断言数量。</summary>
    private static int _checks;

    /// <summary>断言真实界面和流程状态。</summary>
    private static void Check(bool condition, string message)
    {
        _checks++;
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>向实际文本框输入名称并执行确定事件。</summary>
    private static void Rename(TDJS_Vision.Process process, string name)
    {
        using (var form = new FormProcessRename())
        {
            form.SetProcess(process);
            var text = (TextBox)typeof(FormProcessRename).GetField("textBox1", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
            text.Text = name;
            typeof(FormProcessRename).GetMethod("button1_Click", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { null, EventArgs.Empty });
        }
    }

    /// <summary>后台执行真实窗体事件，验证重复改名不会把句点后的名称截断。</summary>
    [STAThread]
    private static int Main()
    {
        Environment.SetEnvironmentVariable("TDJS_VISION_PERFORMANCE_ACCEPTANCE", "1");
        Environment.SetEnvironmentVariable("TDJS_VISION_BACKGROUND_ACCEPTANCE", "1");
        var process = new TDJS_Vision.Process("工位1") { ID = 7 };
        Solution.Instance.AllProcesses.Add(process);
        try
        {
            using (var tabs = new TabControl())
            {
                var page = new TabPage("7.工位1") { Name = "工位1" };
                var otherPage = new TabPage("8.工位1.检测") { Name = "工位1.检测" };
                tabs.TabPages.Add(otherPage);
                tabs.TabPages.Add(page);
                int events = 0;
                EventHandler<(string, string)> handler = (sender, names) => {
                    events++;
                    FormNewProcessWizard.RenameTabPage(tabs, names.Item1, names.Item2);
                };
                FormProcessRename.ProcessRenameChanged += handler;
                try
                {
                    string[] names = { "工位1.初检", "工位1.复检.最终", ".", "普通流程" };
                    foreach (string name in names)
                    {
                        Rename(process, name);
                        Check(process.ProcessName == name, "流程重命名没有保留完整名称：" + name);
                        Check(page.Text == "7." + name, "含句点名称导致选项卡更新错误：" + name);
                        Check(page.Name == name, "选项卡索引名称未同步更新。");
                        Check(otherPage.Text == "8.工位1.检测", "同前缀的其他流程选项卡被错误改名。");
                        Check(ReferenceEquals(Solution.Instance.FindProcess("7." + name), process), "按完整名称无法找到重命名流程。");
                    }
                    foreach (string invalid in new[] { "", "   ", "\t" })
                    {
                        Rename(process, invalid);
                        Check(process.ProcessName == "普通流程" && events == names.Length, "空白名称不应改名或发布改名事件。");
                    }
                }
                finally { FormProcessRename.ProcessRenameChanged -= handler; }
                Rename(process, "无监听.流程");
                Check(process.ProcessName == "无监听.流程", "无监听者时合法改名不应抛异常。");
            }
            Console.WriteLine("PASS: 流程重命名{0}项断言。", _checks);
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
        finally { Solution.Instance.AllProcesses.Remove(process); }
    }
}
